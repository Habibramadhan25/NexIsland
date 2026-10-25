using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public enum RoutineTriggerType
    {
        Manual,
        BatteryBelowThreshold,
        MediaStarted,
        GamingDetected,
        ScheduledTime
    }

    public class RoutineAction
    {
        public string ActionType { get; set; } = string.Empty; // "SwitchModule", "StartFocus", "LaunchApp", "SetEcoMode", "SetBalancedMode", "TogglePlay"
        public string Parameter { get; set; } = string.Empty;
    }

    public class SmartRoutine
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = "GeoControls";
        public bool IsEnabled { get; set; } = true;
        public RoutineTriggerType TriggerType { get; set; } = RoutineTriggerType.Manual;
        public int TriggerValue { get; set; } = 20; // e.g. 20% for battery
        public string ScheduledTime { get; set; } = "09:00"; // e.g. "09:00"
        public List<RoutineAction> Actions { get; set; } = new();
        public DateTime? LastRunTime { get; set; }
        public string LastStatus { get; set; } = "Ready";

        public string TriggerBadge => TriggerType switch
        {
            RoutineTriggerType.BatteryBelowThreshold => $"Battery < {TriggerValue}%",
            RoutineTriggerType.GamingDetected => "Auto on Game Launch",
            RoutineTriggerType.MediaStarted => "Auto on Music Play",
            RoutineTriggerType.ScheduledTime => $"Daily @ {ScheduledTime}",
            _ => "Manual Quick Run"
        };
    }

    public class RoutineService
    {
        private static RoutineService? _instance;
        public static RoutineService Instance => _instance ??= new RoutineService();

        private readonly string _storagePath;
        private readonly DispatcherTimer _scheduleTimer;

        public ObservableCollection<SmartRoutine> Routines { get; } = new();

        public event Action<SmartRoutine>? RoutineTriggered;
        public event Action? RoutinesChanged;

        public RoutineService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "NexIsland");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "routines.json");

            LoadRoutines();

            _scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _scheduleTimer.Tick += OnScheduleTimerTick;
            _scheduleTimer.Start();

            SubscribeToTriggers();
        }

        private void SubscribeToTriggers()
        {
            // Battery threshold trigger
            BatteryService.Instance.BatteryUpdated += bat =>
            {
                if (bat.HasBattery && !bat.IsCharging && bat.Percentage <= 20)
                {
                    var routine = Routines.FirstOrDefault(r => r.IsEnabled && r.TriggerType == RoutineTriggerType.BatteryBelowThreshold && bat.Percentage <= r.TriggerValue);
                    if (routine != null)
                    {
                        var lastRun = routine.LastRunTime ?? DateTime.MinValue;
                        if ((DateTime.Now - lastRun).TotalMinutes > 30) // Debounce 30 mins
                        {
                            _ = ExecuteRoutineAsync(routine, "Triggered by Battery Threshold");
                        }
                    }
                }
            };

            // Gaming mode auto trigger
            GamingModeService.Instance.GamingModeChanged += (active, proc) =>
            {
                if (active)
                {
                    var routine = Routines.FirstOrDefault(r => r.IsEnabled && r.TriggerType == RoutineTriggerType.GamingDetected);
                    if (routine != null)
                    {
                        var lastRun = routine.LastRunTime ?? DateTime.MinValue;
                        if ((DateTime.Now - lastRun).TotalMinutes > 5)
                        {
                            _ = ExecuteRoutineAsync(routine, $"Triggered by Game Launch ({proc})");
                        }
                    }
                }
            };
        }

        private void OnScheduleTimerTick(object? sender, EventArgs e)
        {
            string nowTime = DateTime.Now.ToString("HH:mm");
            foreach (var r in Routines.Where(r => r.IsEnabled && r.TriggerType == RoutineTriggerType.ScheduledTime))
            {
                if (r.ScheduledTime == nowTime)
                {
                    var lastRun = r.LastRunTime ?? DateTime.MinValue;
                    if ((DateTime.Now - lastRun).TotalMinutes > 2)
                    {
                        _ = ExecuteRoutineAsync(r, "Triggered by Scheduled Timer");
                    }
                }
            }
        }

        public async Task ExecuteRoutineAsync(SmartRoutine routine, string reason = "Manual Run", Action<string>? onNavigateModule = null)
        {
            try
            {
                routine.LastStatus = $"Running: {reason}...";
                routine.LastRunTime = DateTime.Now;

                foreach (var action in routine.Actions)
                {
                    switch (action.ActionType)
                    {
                        case "SwitchModule":
                            onNavigateModule?.Invoke(action.Parameter);
                            break;

                        case "StartFocus":
                            FocusTimerService.Instance.StartPomodoro();
                            break;

                        case "SetEcoMode":
                            SystemMonitorService.Instance.SetInterval(TimeSpan.FromSeconds(3));
                            break;

                        case "SetBalancedMode":
                            SystemMonitorService.Instance.SetInterval(TimeSpan.FromSeconds(1));
                            break;

                        case "TogglePlay":
                            await MediaService.Instance.TogglePlayPauseAsync();
                            break;

                        case "LaunchApp":
                            if (!string.IsNullOrWhiteSpace(action.Parameter))
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo { FileName = action.Parameter, UseShellExecute = true });
                                }
                                catch { }
                            }
                            break;
                    }
                }

                routine.LastStatus = $"Completed at {DateTime.Now:HH:mm:ss}";
                RoutineTriggered?.Invoke(routine);
                NotificationService.Instance.PostNotification(routine.Name, $"Smart Routine executed successfully ({reason}).", "NexIsland Routines", "🤖");
                SaveRoutines();
            }
            catch (Exception ex)
            {
                routine.LastStatus = $"Error: {ex.Message}";
                LoggingService.Instance.Error($"Error executing routine {routine.Name}", ex);
            }
        }

        private void LoadRoutines()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    var loaded = JsonSerializer.Deserialize<List<SmartRoutine>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        Routines.Clear();
                        foreach (var item in loaded) Routines.Add(item);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Note loading routines: {ex.Message}");
            }

            // Defaults if file didn't exist or was empty
            PopulateDefaultRoutines();
            SaveRoutines();
        }

        private void PopulateDefaultRoutines()
        {
            Routines.Clear();

            // 1. Study Mode
            Routines.Add(new SmartRoutine
            {
                Name = "Study Mode",
                Description = "Activate 25m Focus Pomodoro, open agenda, and set distraction-free island",
                IconKey = "GeoFocus",
                TriggerType = RoutineTriggerType.Manual,
                Actions = new List<RoutineAction>
                {
                    new() { ActionType = "StartFocus" },
                    new() { ActionType = "SwitchModule", Parameter = "Focus" },
                    new() { ActionType = "SetBalancedMode" }
                }
            });

            // 2. Gaming Optimization Mode
            Routines.Add(new SmartRoutine
            {
                Name = "Gaming Optimization",
                Description = "Reduce polling to Eco profile, suppress alerts, and monitor hardware",
                IconKey = "GeoGame",
                TriggerType = RoutineTriggerType.GamingDetected,
                Actions = new List<RoutineAction>
                {
                    new() { ActionType = "SetEcoMode" },
                    new() { ActionType = "SwitchModule", Parameter = "System" }
                }
            });

            // 3. Music Chill Mode
            Routines.Add(new SmartRoutine
            {
                Name = "Music Chillout",
                Description = "Jump to Music Player, resume playback, and showcase live lyrics & visualizer",
                IconKey = "GeoMusic",
                TriggerType = RoutineTriggerType.Manual,
                Actions = new List<RoutineAction>
                {
                    new() { ActionType = "TogglePlay" },
                    new() { ActionType = "SwitchModule", Parameter = "Music" }
                }
            });

            // 4. Smart Battery Saver
            Routines.Add(new SmartRoutine
            {
                Name = "Smart Battery Saver",
                Description = "Triggered when battery falls below 20%: reduces polling and switches to Power view",
                IconKey = "GeoBattery",
                TriggerType = RoutineTriggerType.BatteryBelowThreshold,
                TriggerValue = 20,
                Actions = new List<RoutineAction>
                {
                    new() { ActionType = "SetEcoMode" },
                    new() { ActionType = "SwitchModule", Parameter = "Battery" }
                }
            });

            // 5. Work Deep Focus
            Routines.Add(new SmartRoutine
            {
                Name = "Daily Work Focus",
                Description = "Scheduled at 09:00: starts workday focus session and opens Agenda",
                IconKey = "GeoCalendar",
                TriggerType = RoutineTriggerType.ScheduledTime,
                ScheduledTime = "09:00",
                Actions = new List<RoutineAction>
                {
                    new() { ActionType = "SwitchModule", Parameter = "Calendar" },
                    new() { ActionType = "SetBalancedMode" }
                }
            });
        }

        public void SaveRoutines()
        {
            try
            {
                var json = JsonSerializer.Serialize(Routines.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storagePath, json);
                RoutinesChanged?.Invoke();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to save smart routines", ex);
            }
        }

        public void AddRoutine(SmartRoutine routine)
        {
            Routines.Add(routine);
            SaveRoutines();
        }

        public void DeleteRoutine(string id)
        {
            var r = Routines.FirstOrDefault(x => x.Id == id);
            if (r != null)
            {
                Routines.Remove(r);
                SaveRoutines();
            }
        }
    }
}
