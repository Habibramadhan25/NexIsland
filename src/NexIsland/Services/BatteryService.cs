using System;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public class BatteryInfo
    {
        public bool HasBattery { get; set; }
        public int Percentage { get; set; }
        public bool IsCharging { get; set; }
        public bool IsAcOnline { get; set; }
        public bool IsBatterySaver { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string StatusSummary { get; set; } = "Unknown";
    }

    public class BatteryService
    {
        private static BatteryService? _instance;
        public static BatteryService Instance => _instance ??= new BatteryService();

        private readonly DispatcherTimer _timer;
        private BatteryInfo _current = new();
        private bool? _lastAcOnline;
        private bool _lowBatteryAlertFired;

        public event Action<BatteryInfo>? BatteryUpdated;
        public event Action<string>? BatteryAlert;

        public BatteryInfo Current => _current;

        public BatteryService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _timer.Tick += (s, e) => UpdateBatteryStatus();
        }

        public void Start()
        {
            UpdateBatteryStatus();
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

        public void UpdateBatteryStatus()
        {
            if (NativeMethods.GetSystemPowerStatus(out var status))
            {
                bool hasBattery = (status.BatteryFlag & 128) == 0 && status.BatteryLifePercent != 255;
                bool isAcOnline = status.ACLineStatus == 1;
                bool isCharging = (status.BatteryFlag & 8) != 0;
                int percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : 0;
                bool batterySaver = status.SystemStatusFlag == 1;
                TimeSpan? estimatedTime = status.BatteryLifeTime > 0 ? TimeSpan.FromSeconds(status.BatteryLifeTime) : null;

                string summary;
                if (!hasBattery)
                {
                    summary = isAcOnline ? "Desktop (AC Power)" : "No Battery Detected";
                }
                else if (isCharging)
                {
                    summary = $"Charging: {percent}%";
                }
                else
                {
                    summary = $"{percent}% ({(isAcOnline ? "Plugged in" : "On Battery")})";
                }

                _current = new BatteryInfo
                {
                    HasBattery = hasBattery,
                    Percentage = percent,
                    IsCharging = isCharging,
                    IsAcOnline = isAcOnline,
                    IsBatterySaver = batterySaver,
                    EstimatedTimeRemaining = estimatedTime,
                    StatusSummary = summary
                };

                // Power state transition notifications
                if (_lastAcOnline.HasValue && _lastAcOnline.Value != isAcOnline)
                {
                    var msg = isAcOnline ? "AC Power connected (Charging)" : "AC Power disconnected (Running on battery)";
                    BatteryAlert?.Invoke(msg);
                }
                _lastAcOnline = isAcOnline;

                // Low battery alert
                if (hasBattery && !isCharging && percent <= 20)
                {
                    if (!_lowBatteryAlertFired)
                    {
                        BatteryAlert?.Invoke($"Battery Low: {percent}% remaining. Please plug in charger.");
                        _lowBatteryAlertFired = true;
                    }
                }
                else
                {
                    _lowBatteryAlertFired = false;
                }

                BatteryUpdated?.Invoke(_current);
            }
        }
    }
}
