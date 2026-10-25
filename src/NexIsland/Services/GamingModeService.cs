using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public class GamingModeService
    {
        private static GamingModeService? _instance;
        public static GamingModeService Instance => _instance ??= new GamingModeService();

        private readonly DispatcherTimer _timer;
        private bool _isGamingActive;
        private string _activeProcessName = string.Empty;

        public bool IsGamingActive => _isGamingActive;
        public string ActiveProcessName => _activeProcessName;

        public event Action<bool, string>? GamingModeChanged;

        public GamingModeService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3) // Lightweight check every 3s
            };
            _timer.Tick += (s, e) => CheckForegroundApp();
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void CheckForegroundApp()
        {
            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return;

                string procName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName.ToLowerInvariant();
                }
                catch { }

                _activeProcessName = procName;

                // Check if process matches known game list or if window is full screen
                var settings = Settings.SettingsService.Instance.Current;
                bool isGameProcess = settings.KnownGameProcesses.Any(g => procName.Contains(g, StringComparison.OrdinalIgnoreCase));
                bool isFullScreen = IsWindowFullScreen(hwnd);

                bool gamingNow = isGameProcess || (isFullScreen && procName != "explorer");

                if (gamingNow != _isGamingActive)
                {
                    _isGamingActive = gamingNow;
                    GamingModeChanged?.Invoke(_isGamingActive, procName);
                    LoggingService.Instance.Info($"Gaming mode {(_isGamingActive ? "activated" : "deactivated")} for process: {procName}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Foreground check exception: {ex.Message}");
            }
        }

        private static bool IsWindowFullScreen(IntPtr hwnd)
        {
            if (NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                var hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                var mi = new NativeMethods.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    // If window equals or exceeds monitor size, it is running in borderless or exclusive fullscreen
                    return rect.Left <= mi.rcMonitor.Left &&
                           rect.Top <= mi.rcMonitor.Top &&
                           rect.Right >= mi.rcMonitor.Right &&
                           rect.Bottom >= mi.rcMonitor.Bottom;
                }
            }
            return false;
        }
    }
}
