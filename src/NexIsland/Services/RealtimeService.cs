using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public enum ModuleLiveStatus
    {
        Live,
        Updating,
        Offline,
        Unavailable
    }

    public class ModuleStatusInfo
    {
        public ModuleLiveStatus Status { get; set; } = ModuleLiveStatus.Offline;
        public string Detail { get; set; } = "Initializing";
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public string StatusBadgeText => Status switch
        {
            ModuleLiveStatus.Live => "LIVE",
            ModuleLiveStatus.Updating => "SYNCING",
            ModuleLiveStatus.Offline => "OFFLINE",
            ModuleLiveStatus.Unavailable => "UNAVAILABLE",
            _ => "IDLE"
        };

        public string StatusColorHex => Status switch
        {
            ModuleLiveStatus.Live => "#10B981",       // Emerald Green
            ModuleLiveStatus.Updating => "#3B82F6",   // Electric Blue
            ModuleLiveStatus.Offline => "#64748B",    // Slate Gray
            ModuleLiveStatus.Unavailable => "#F59E0B",// Amber Warning
            _ => "#64748B"
        };
    }

    /// <summary>
    /// Realtime Core Engine coordinating live data sources, adaptive update intervals,
    /// module connection states (Live, Updating, Offline, Unavailable), and safe resource release.
    /// </summary>
    public class RealtimeService : IDisposable
    {
        private static RealtimeService? _instance;
        public static RealtimeService Instance => _instance ??= new RealtimeService();

        private readonly ConcurrentDictionary<string, ModuleStatusInfo> _moduleStatuses = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly DispatcherTimer _adaptiveHeartbeatTimer;
        private bool _isDisposed;
        private bool _isIslandExpanded;
        private string _activeModuleName = "Music";

        public event Action<string, ModuleStatusInfo>? StatusChanged;

        public RealtimeService()
        {
            // Initialize known module statuses with sensible baselines
            SetStatus("Music", ModuleLiveStatus.Updating, "Connecting to Windows Media Session (GSMTC)...");
            SetStatus("System", ModuleLiveStatus.Live, "Win32 Kernel Metrics (GetSystemTimes)");
            SetStatus("Focus", ModuleLiveStatus.Live, "High-Precision Productivity Timer");
            SetStatus("Lyrics", ModuleLiveStatus.Updating, "Synchronizing Track Timestamps");
            SetStatus("Battery", ModuleLiveStatus.Live, "Win32 GetSystemPowerStatus");
            SetStatus("Network", ModuleLiveStatus.Live, "NetworkInterface Hardware Sampling");
            SetStatus("Ai", ModuleLiveStatus.Live, "NexIsland Copilot Engine (DPAPI Protected)");
            SetStatus("Search", ModuleLiveStatus.Live, "Universal Indexer (Start Menu, Settings, Shell)");
            SetStatus("Routines", ModuleLiveStatus.Live, "Automated Activity Triggers Active");
            SetStatus("Launcher", ModuleLiveStatus.Live, "Windows Shell Shortcuts");
            SetStatus("Notifications", ModuleLiveStatus.Live, "Windows Toast & Notification Feed");
            SetStatus("Weather", ModuleLiveStatus.Updating, "Open-Meteo Satellite & Station API");
            SetStatus("Calendar", ModuleLiveStatus.Live, "Local Agenda & Reminders Store");
            SetStatus("Clipboard", ModuleLiveStatus.Live, "Win32 AddClipboardFormatListener (Event-Driven)");
            SetStatus("Visualizer", ModuleLiveStatus.Live, "WASAPI Hardware Audio Loopback Capture");
            SetStatus("Controls", ModuleLiveStatus.Live, "Windows System Settings Dispatcher");
            SetStatus("Gaming", ModuleLiveStatus.Live, "Low-Latency Foreground Process Watcher");
            SetStatus("FileDrop", ModuleLiveStatus.Live, "Direct OLE Drag-and-Drop Workspace");
            SetStatus("Activity", ModuleLiveStatus.Live, "Real-time Task Progress Dispatcher");
            SetStatus("Themes", ModuleLiveStatus.Live, "Dynamic Theme & Module Reordering");
            SetStatus("Plugins", ModuleLiveStatus.Live, "Plugin Extensibility Sandbox");

            // Adaptive heartbeat timer to regulate polling rates according to Island state
            _adaptiveHeartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _adaptiveHeartbeatTimer.Tick += OnAdaptiveHeartbeat;
            _adaptiveHeartbeatTimer.Start();
        }

        public void SetIslandState(bool isExpanded, string activeModuleName)
        {
            _isIslandExpanded = isExpanded;
            _activeModuleName = activeModuleName;
            AdjustPollingIntervals();
        }

        private void AdjustPollingIntervals()
        {
            try
            {
                if (!_isIslandExpanded)
                {
                    // In compact mode: reduce polling rates to conserve CPU and battery
                    SystemMonitorService.Instance.SetInterval(TimeSpan.FromSeconds(3));
                    BatteryService.Instance.SetInterval(TimeSpan.FromSeconds(10));
                    
                    // If visualizer is not in view and not playing, audio visualizer can idle
                    if (!MediaService.Instance.CurrentTrack.IsPlaying)
                    {
                        AudioVisualizerService.Instance.Stop();
                    }
                }
                else
                {
                    // In expanded mode: responsive real-time data
                    if (_activeModuleName == "System" || _activeModuleName == "Gaming")
                    {
                        SystemMonitorService.Instance.SetInterval(TimeSpan.FromMilliseconds(800));
                    }
                    else
                    {
                        SystemMonitorService.Instance.SetInterval(TimeSpan.FromSeconds(1.5));
                    }

                    BatteryService.Instance.SetInterval(TimeSpan.FromSeconds(3));

                    if (_activeModuleName == "Visualizer" || _activeModuleName == "Music")
                    {
                        AudioVisualizerService.Instance.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Adaptive polling adjustment note: {ex.Message}");
            }
        }

        private void OnAdaptiveHeartbeat(object? sender, EventArgs e)
        {
            // Verify and refresh status of services
            try
            {
                // Verify Media Service GSMTC status
                var media = MediaService.Instance.CurrentTrack;
                if (media.IsPlaying)
                {
                    SetStatus("Music", ModuleLiveStatus.Live, $"{media.Title} • {media.Artist}");
                }
                else if (!string.IsNullOrEmpty(media.Title) && media.Title != "No media playing")
                {
                    SetStatus("Music", ModuleLiveStatus.Live, "Paused");
                }
                else
                {
                    SetStatus("Music", ModuleLiveStatus.Offline, "No Active Windows Media Session");
                }

                // Verify Battery status
                var bat = BatteryService.Instance.Current;
                if (bat.HasBattery)
                {
                    SetStatus("Battery", ModuleLiveStatus.Live, $"{bat.Percentage}% • {(bat.IsCharging ? "Charging" : "On Battery")}");
                }
                else
                {
                    SetStatus("Battery", ModuleLiveStatus.Live, "AC Desktop Power (No Battery Required)");
                }

                // Verify Audio Visualizer WASAPI capture
                bool visActive = AudioVisualizerService.Instance.IsActive;
                SetStatus("Visualizer", visActive ? ModuleLiveStatus.Live : ModuleLiveStatus.Offline,
                    visActive ? "WASAPI Loopback Active (Live Hardware PCM)" : "Audio Capture Idle");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Heartbeat check: {ex.Message}");
            }
        }

        public ModuleStatusInfo GetStatus(string moduleName)
        {
            if (_moduleStatuses.TryGetValue(moduleName, out var info))
            {
                return info;
            }
            return new ModuleStatusInfo { Status = ModuleLiveStatus.Offline, Detail = "Not Registered" };
        }

        public void SetStatus(string moduleName, ModuleLiveStatus status, string detail)
        {
            var info = _moduleStatuses.GetOrAdd(moduleName, _ => new ModuleStatusInfo());
            info.Status = status;
            info.Detail = detail;
            info.LastUpdated = DateTime.Now;

            StatusChanged?.Invoke(moduleName, info);
        }

        public CancellationToken GetCancellationToken() => _cts.Token;

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _cts.Cancel();
                _cts.Dispose();
                _adaptiveHeartbeatTimer.Stop();
            }
            catch { }
        }
    }
}
