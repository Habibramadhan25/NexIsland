using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public class SystemMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double RamUsagePercent { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public double DiskUsagePercent { get; set; }
        public double DiskUsedGb { get; set; }
        public double DiskTotalGb { get; set; }
        public double DiskFreeGb => Math.Max(0, DiskTotalGb - DiskUsedGb);
        public double NetworkDownloadKbps { get; set; }
        public double NetworkUploadKbps { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class SystemMonitorService
    {
        private static SystemMonitorService? _instance;
        public static SystemMonitorService Instance => _instance ??= new SystemMonitorService();

        private readonly DispatcherTimer _timer;
        private readonly Queue<double> _cpuHistory = new();
        private readonly Queue<double> _ramHistory = new();
        private const int MaxHistoryPoints = 30;

        private ulong _prevIdleTime;
        private ulong _prevKernelTime;
        private ulong _prevUserTime;
        private bool _firstCpuSample = true;

        private long _prevBytesReceived;
        private long _prevBytesSent;
        private DateTime _prevNetworkTime = DateTime.MinValue;

        public event Action<SystemMetrics>? MetricsUpdated;
        public SystemMetrics CurrentMetrics { get; private set; } = new();
        public IReadOnlyCollection<double> CpuHistory => _cpuHistory;
        public IReadOnlyCollection<double> RamHistory => _ramHistory;

        public SystemMonitorService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _timer.Tick += (s, e) => CollectMetrics();
        }

        public void Start()
        {
            CollectMetrics();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void SetInterval(TimeSpan interval)
        {
            _timer.Interval = interval;
        }

        public void CollectMetrics()
        {
            var cpu = CalculateCpuUsage();
            var (ramPercent, ramUsedGb, ramTotalGb) = GetRamUsage();
            var (diskPercent, diskUsedGb, diskTotalGb) = GetDiskUsage();
            var (downloadKbps, uploadKbps) = CalculateNetworkSpeed();

            CurrentMetrics = new SystemMetrics
            {
                CpuUsagePercent = Math.Round(cpu, 1),
                RamUsagePercent = Math.Round(ramPercent, 1),
                RamUsedGb = Math.Round(ramUsedGb, 1),
                RamTotalGb = Math.Round(ramTotalGb, 1),
                DiskUsagePercent = Math.Round(diskPercent, 1),
                DiskUsedGb = Math.Round(diskUsedGb, 1),
                DiskTotalGb = Math.Round(diskTotalGb, 1),
                NetworkDownloadKbps = Math.Round(downloadKbps, 1),
                NetworkUploadKbps = Math.Round(uploadKbps, 1),
                Timestamp = DateTime.Now
            };

            // Update history queues
            if (_cpuHistory.Count >= MaxHistoryPoints) _cpuHistory.Dequeue();
            _cpuHistory.Enqueue(CurrentMetrics.CpuUsagePercent);

            if (_ramHistory.Count >= MaxHistoryPoints) _ramHistory.Dequeue();
            _ramHistory.Enqueue(CurrentMetrics.RamUsagePercent);

            RealtimeService.Instance.SetStatus("System", ModuleLiveStatus.Live,
                $"CPU: {CurrentMetrics.CpuUsagePercent:F0}% • RAM: {CurrentMetrics.RamUsagePercent:F0}% ({CurrentMetrics.RamUsedGb:F1}/{CurrentMetrics.RamTotalGb:F0} GB)");

            MetricsUpdated?.Invoke(CurrentMetrics);
        }

        private double CalculateCpuUsage()
        {
            if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return 0;
            }

            ulong idleTime = idle.ToULong();
            ulong kernelTime = kernel.ToULong();
            ulong userTime = user.ToULong();

            if (_firstCpuSample)
            {
                _prevIdleTime = idleTime;
                _prevKernelTime = kernelTime;
                _prevUserTime = userTime;
                _firstCpuSample = false;
                return 0;
            }

            ulong diffIdle = idleTime - _prevIdleTime;
            ulong diffKernel = kernelTime - _prevKernelTime;
            ulong diffUser = userTime - _prevUserTime;

            _prevIdleTime = idleTime;
            _prevKernelTime = kernelTime;
            _prevUserTime = userTime;

            ulong totalSys = diffKernel + diffUser;
            if (totalSys == 0) return 0;

            ulong totalActive = totalSys > diffIdle ? totalSys - diffIdle : 0;
            double percent = (double)totalActive * 100.0 / totalSys;
            return Math.Clamp(percent, 0.0, 100.0);
        }

        private (double percent, double usedGb, double totalGb) GetRamUsage()
        {
            var mem = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(mem))
            {
                double total = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
                double avail = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
                double used = total - avail;
                return (mem.dwMemoryLoad, used, total);
            }
            return (0, 0, 0);
        }

        private (double percent, double usedGb, double totalGb) GetDiskUsage()
        {
            try
            {
                var systemDrive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
                if (systemDrive != null)
                {
                    double total = systemDrive.TotalSize / (1024.0 * 1024 * 1024);
                    double free = systemDrive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    double used = total - free;
                    double percent = total > 0 ? (used / total) * 100.0 : 0;
                    return (Math.Round(percent, 1), Math.Round(used, 1), Math.Round(total, 1));
                }
            }
            catch { }
            return (0, 0, 0);
        }

        private (double downloadKbps, double uploadKbps) CalculateNetworkSpeed()
        {
            try
            {
                long totalRecv = 0;
                long totalSent = 0;

                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var nic in interfaces)
                {
                    var stats = nic.GetIPv4Statistics();
                    totalRecv += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                }

                var now = DateTime.Now;
                if (_prevNetworkTime == DateTime.MinValue)
                {
                    _prevNetworkTime = now;
                    _prevBytesReceived = totalRecv;
                    _prevBytesSent = totalSent;
                    return (0, 0);
                }

                double elapsedSec = (now - _prevNetworkTime).TotalSeconds;
                if (elapsedSec <= 0) return (0, 0);

                long diffRecv = totalRecv - _prevBytesReceived;
                long diffSent = totalSent - _prevBytesSent;

                _prevNetworkTime = now;
                _prevBytesReceived = totalRecv;
                _prevBytesSent = totalSent;

                double dlSpeed = (diffRecv / 1024.0) / elapsedSec;
                double upSpeed = (diffSent / 1024.0) / elapsedSec;

                return (Math.Max(0, dlSpeed), Math.Max(0, upSpeed));
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
