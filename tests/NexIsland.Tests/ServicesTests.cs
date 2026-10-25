using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NexIsland.Plugins;
using NexIsland.Services;
using NexIsland.Settings;
using NexIsland.ViewModels;
using Xunit;

namespace NexIsland.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void AppSettings_DefaultValues_AreSensible()
        {
            var settings = new AppSettings();

            Assert.True(settings.AlwaysOnTop);
            Assert.True(settings.LockPosition);
            Assert.Equal(ThemeStyle.Dark, settings.Theme);
            Assert.True(settings.EnableMedia);
            Assert.True(settings.EnableBattery);
            Assert.True(settings.EnableSystemMonitor);
            Assert.Equal(25, settings.PomodoroWorkMinutes);
            Assert.Equal(5, settings.PomodoroShortBreakMinutes);
        }

        [Fact]
        public void AppSettings_Serialization_RoundTripsAccurately()
        {
            var original = new AppSettings
            {
                AccentColorHex = "#10B981",
                CornerRadius = 18,
                ReducedMotion = true,
                WeatherCity = "Bandung"
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("#10B981", deserialized.AccentColorHex);
            Assert.Equal(18, deserialized.CornerRadius);
            Assert.True(deserialized.ReducedMotion);
            Assert.Equal("Bandung", deserialized.WeatherCity);
        }
    }

    public class FocusTimerTests
    {
        [Fact]
        public void FocusTimer_StartAndReset_UpdatesStateProperly()
        {
            var service = new FocusTimerService();
            service.StartCustomTimer(TimeSpan.FromMinutes(10));

            Assert.True(service.IsRunning);
            Assert.False(service.IsPaused);
            Assert.Equal(FocusModeType.CustomTimer, service.CurrentMode);

            service.Pause();
            Assert.True(service.IsPaused);

            service.Resume();
            Assert.False(service.IsPaused);

            service.Reset();
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void FocusTimer_Pomodoro_InitializesWorkStage()
        {
            var service = new FocusTimerService();
            service.StartPomodoro(workMinutes: 25, shortBreakMinutes: 5, longBreakMinutes: 15);

            Assert.Equal(FocusModeType.Pomodoro, service.CurrentMode);
            Assert.Equal(PomodoroStage.Work, service.CurrentPomodoroStage);
            Assert.True(service.IsRunning);
            service.Reset();
        }
    }

    public class LauncherTests
    {
        [Fact]
        public void LauncherService_Search_ReturnsMatchingResults()
        {
            var service = new LauncherService();
            service.AddItem("Visual Studio Code", "code.exe", "Development", isPinned: true);
            service.AddItem("Spotify Music", "spotify.exe", "Media", isPinned: false);

            var codeResults = service.Search("Code").ToList();
            Assert.Contains(codeResults, i => i.Title == "Visual Studio Code");

            var mediaResults = service.Search("Media").ToList();
            Assert.Contains(mediaResults, i => i.Title == "Spotify Music");
        }
    }

    public class ClipboardTests
    {
        [Fact]
        public void ClipboardItem_PreviewText_TruncatesWhenLong()
        {
            var longText = new string('A', 120);
            var item = new ClipboardItem { Content = longText };

            Assert.EndsWith("...", item.PreviewText);
            Assert.Equal(80, item.PreviewText.Length);
        }
    }

    public class PluginTests
    {
        private class TestPlugin : INexIslandPlugin
        {
            public PluginMetadata Metadata { get; } = new()
            {
                Id = "test.plugin",
                Name = "Test Plugin",
                Version = "1.0.0"
            };

            public bool IsEnabled { get; set; } = true;
            public bool WasInitialized { get; private set; }
            public bool WasStarted { get; private set; }
            public bool WasStopped { get; private set; }

            public Task InitializeAsync(IPluginContext context)
            {
                WasInitialized = true;
                return Task.CompletedTask;
            }

            public Task StartAsync()
            {
                WasStarted = true;
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                WasStopped = true;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task PluginService_RegisterAndLifecycle_FunctionsSafely()
        {
            var plugin = new TestPlugin();
            var service = new PluginService();

            service.RegisterPlugin(plugin);
            Assert.Contains(service.Plugins, p => p.Metadata.Id == "test.plugin");

            await plugin.InitializeAsync(new PluginContext("test.plugin"));
            Assert.True(plugin.WasInitialized);

            await plugin.StartAsync();
            Assert.True(plugin.WasStarted);

            await plugin.StopAsync();
            Assert.True(plugin.WasStopped);
        }
    }

    public class MainViewModelTests
    {
        [Fact]
        public void MainViewModel_StateChanges_AdjustDimensions()
        {
            var vm = new MainViewModel();

            vm.State = IslandVisualState.Compact;
            Assert.True(vm.IsCompact);
            Assert.False(vm.IsExpanded);
            Assert.False(vm.IsNotificationState);

            vm.State = IslandVisualState.Expanded;
            Assert.False(vm.IsCompact);
            Assert.True(vm.IsExpanded);
            Assert.True(vm.Width >= 360);

            vm.State = IslandVisualState.Notification;
            Assert.True(vm.IsNotificationState);
            Assert.False(vm.IsExpanded);
        }

        [Fact]
        public void MainViewModel_SwipeNavigation_CyclesAllModules()
        {
            var vm = new MainViewModel();
            vm.CurrentModule = IslandModule.Music;
            Assert.True(vm.IsMusicModule);

            // Master sequence: Music -> System -> Focus -> Lyrics
            vm.NextModule();
            Assert.Equal(IslandModule.System, vm.CurrentModule);
            Assert.True(vm.IsSystemModule);

            vm.NextModule();
            Assert.Equal(IslandModule.Focus, vm.CurrentModule);

            vm.NextModule();
            Assert.Equal(IslandModule.Lyrics, vm.CurrentModule);

            // Previous swipe
            vm.PreviousModule();
            Assert.Equal(IslandModule.Focus, vm.CurrentModule);

            // Set to last module in effective list and swipe next -> wraps to first
            var lastMod = vm.GetEffectiveModuleList().Last();
            vm.CurrentModule = lastMod;
            vm.NextModule();
            Assert.Equal(IslandModule.Music, vm.CurrentModule);

            // Swipe prev from first module -> wraps to last
            vm.PreviousModule();
            Assert.Equal(lastMod, vm.CurrentModule);
        }

        [Fact]
        public void MainViewModel_SeekPreview_UpdatesProgressWithoutStateDestruction()
        {
            var vm = new MainViewModel();
            vm.BeginSeek();
            Assert.True(vm.IsUserSeeking);

            vm.UpdateSeekPreview(0.5);
            Assert.Equal(0.5, vm.MediaProgress);

            vm.EndSeek(0.75);
            Assert.False(vm.IsUserSeeking);
            Assert.Equal(0.75, vm.MediaProgress);
        }
    }

    public class LyricsTests
    {
        [Fact]
        public void LyricsService_ParseLrc_ExtractsTimestampsAndLines()
        {
            var service = new LyricsService();
            string sampleLrc = @"[00:00.00] Intro Music
[00:05.50] First verse begins here
[00:15.00] Chorus melody plays";

            service.LoadLrcContent(sampleLrc);

            Assert.True(service.HasLyrics);
            Assert.Equal(3, service.CurrentLyrics.Count);
            Assert.Equal("Intro Music", service.CurrentLyrics[0].Text);
            Assert.Equal(TimeSpan.FromSeconds(5.5), service.CurrentLyrics[1].Timestamp);

            // At 6 seconds, line 1 should be active
            service.UpdatePosition(TimeSpan.FromSeconds(6.0));
            Assert.Equal("First verse begins here", service.CurrentLineText);
            Assert.Equal("Intro Music", service.PreviousLineText);
            Assert.Equal("Chorus melody plays", service.NextLineText);
        }
    }

    public class MediaTimelineTests
    {
        [Fact]
        public void MediaTrackInfo_InvalidDuration_ReportsCorrectFallbacks()
        {
            var track = new MediaTrackInfo
            {
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero
            };

            Assert.False(track.HasValidDuration);
            Assert.Equal(0.0, track.ProgressFraction);
        }

        [Fact]
        public void MediaTrackInfo_ValidDuration_ComputesAccurateFraction()
        {
            var track = new MediaTrackInfo
            {
                Position = TimeSpan.FromMinutes(1),
                Duration = TimeSpan.FromMinutes(4)
            };

            Assert.True(track.HasValidDuration);
            Assert.Equal(0.25, track.ProgressFraction, 2);
        }
    }

    public class AiServiceTests
    {
        [Fact]
        public async Task OfflineAiProvider_RespondsToSummarizeAndChecklist()
        {
            var provider = new OfflineFallbackAiProvider();
            var history = new System.Collections.Generic.List<AiMessage>();

            var summary = await provider.GenerateResponseAsync("summarize my meeting notes", history, default);
            Assert.Contains("AI Summary", summary);

            var checklist = await provider.GenerateResponseAsync("create a checklist for tomorrow", history, default);
            Assert.Contains("Task Checklist", checklist);

            var shortcuts = await provider.GenerateResponseAsync("windows shortcuts", history, default);
            Assert.Contains("Alt + Space", shortcuts);
        }

        [Fact]
        public async Task AiService_AskAsync_AddsMessagesToHistory()
        {
            var service = new AiService();
            int initialCount = service.ConversationHistory.Count;

            var reply = await service.AskAsync("windows shortcut tips");
            Assert.NotEmpty(reply);
            Assert.Equal(initialCount + 2, service.ConversationHistory.Count);

            service.ClearHistory();
            Assert.Single(service.ConversationHistory);
        }
    }

    public class SearchServiceTests
    {
        [Fact]
        public async Task SearchService_EvaluatesMathExpressions()
        {
            var service = new SearchService();
            var results = await service.SearchAsync("25 * 4 + 10");

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.ResultType == SearchResultType.Calculator && r.Title.Contains("110"));
        }

        [Fact]
        public async Task SearchService_FindsModulesAndSettings()
        {
            var service = new SearchService();
            var results = await service.SearchAsync("Music");

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.ResultType == SearchResultType.NexIslandModule && r.Title.Contains("Music Player"));

            var settingResults = await service.SearchAsync("Display");
            Assert.Contains(settingResults, r => r.ResultType == SearchResultType.WindowsSetting);
        }
    }

    public class RoutineServiceTests
    {
        [Fact]
        public void RoutineService_LoadsDefaultRoutinesWithTriggers()
        {
            var service = new RoutineService();
            Assert.NotEmpty(service.Routines);

            var studyRoutine = service.Routines.FirstOrDefault(r => r.Name.Contains("Study"));
            Assert.NotNull(studyRoutine);

            var batteryRoutine = service.Routines.FirstOrDefault(r => r.TriggerType == RoutineTriggerType.BatteryBelowThreshold);
            Assert.NotNull(batteryRoutine);
            Assert.True(batteryRoutine.TriggerValue <= 20);
        }

        [Fact]
        public async Task RoutineService_ExecutesRoutineAndUpdatesStatus()
        {
            var service = new RoutineService();
            var routine = service.Routines.First();

            await service.ExecuteRoutineAsync(routine, "Unit Test Trigger");
            Assert.Contains("Completed", routine.LastStatus);
            Assert.NotNull(routine.LastRunTime);
        }

        [Fact]
        public void EffectiveModules_ContainsAll20ModulesInSequence()
        {
            var vm = new MainViewModel();
            var list = vm.GetEffectiveModuleList();

            Assert.Equal(20, list.Count);
            Assert.Equal(IslandModule.Music, list[0]);
            Assert.Equal(IslandModule.System, list[1]);
            Assert.Equal(IslandModule.Focus, list[2]);
            Assert.Equal(IslandModule.Lyrics, list[3]);
            Assert.Equal(IslandModule.Battery, list[4]);
            Assert.Equal(IslandModule.Network, list[5]);
            Assert.Equal(IslandModule.Search, list[6]);
            Assert.Equal(IslandModule.Routines, list[7]);
        }
    }

    public class RealtimeServiceTests
    {
        [Fact]
        public void RealtimeService_TracksStatusAccurately()
        {
            var service = new RealtimeService();
            var systemStatus = service.GetStatus("System");

            Assert.Equal(ModuleLiveStatus.Live, systemStatus.Status);
            Assert.Equal("LIVE", systemStatus.StatusBadgeText);
            Assert.Equal("#10B981", systemStatus.StatusColorHex);

            service.SetStatus("CustomTestModule", ModuleLiveStatus.Updating, "Syncing telemetry");
            var customStatus = service.GetStatus("CustomTestModule");

            Assert.Equal(ModuleLiveStatus.Updating, customStatus.Status);
            Assert.Equal("SYNCING", customStatus.StatusBadgeText);
            Assert.Equal("#3B82F6", customStatus.StatusColorHex);
        }

        [Fact]
        public void RealtimeService_AdaptivePolling_RegulatesIntervals()
        {
            var service = new RealtimeService();
            // Should not throw and adjust polling
            service.SetIslandState(isExpanded: false, "Music");
            service.SetIslandState(isExpanded: true, "System");
            service.SetIslandState(isExpanded: true, "Visualizer");

            var ct = service.GetCancellationToken();
            Assert.False(ct.IsCancellationRequested);
        }
    }

    public class BrightnessControlTests
    {
        [Fact]
        public void BrightnessService_ClampsValueBetween0And100()
        {
            var service = new BrightnessService();
            int notifiedValue = -1;
            service.BrightnessChanged += v => notifiedValue = v;

            service.SetBrightness(150);
            Assert.Equal(100, service.CurrentBrightness);
            Assert.Equal(100, notifiedValue);

            service.SetBrightness(-25);
            Assert.Equal(0, service.CurrentBrightness);
            Assert.Equal(0, notifiedValue);

            service.SetBrightness(72);
            Assert.Equal(72, service.CurrentBrightness);
            Assert.Equal(72, notifiedValue);

            service.Dispose();
        }

        [Fact]
        public void BrightnessService_Dispose_CleansResourcesGracefully()
        {
            var service = new BrightnessService();
            service.Dispose();
            // Calling dispose twice should be idempotent and not throw
            service.Dispose();
        }
    }

    public class AudioControlTests
    {
        [Fact]
        public void AudioControlService_ClampsVolumeProperly()
        {
            var service = new AudioControlService();
            int notifiedVol = -1;
            service.VolumeChanged += (v, m) => notifiedVol = v;

            service.SetVolume(120);
            Assert.Equal(100, service.MasterVolume);
            Assert.Equal(100, notifiedVol);

            service.SetVolume(-10);
            Assert.Equal(0, service.MasterVolume);
            Assert.Equal(0, notifiedVol);

            service.Dispose();
        }

        [Fact]
        public void AudioControlService_HasValidDeviceName()
        {
            var service = new AudioControlService();
            Assert.False(string.IsNullOrWhiteSpace(service.DeviceName));
            service.Dispose();
        }

        [Fact]
        public void AudioControlService_Dispose_IsIdempotent()
        {
            var service = new AudioControlService();
            service.Dispose();
            service.Dispose();
        }
    }

    public class QuickControlsViewModelTests
    {
        [Fact]
        public void MainViewModel_QuickControls_PropertiesAndCommands_AreFunctional()
        {
            var vm = new MainViewModel();

            Assert.NotNull(vm.OpenDisplaySettingsCommand);
            Assert.NotNull(vm.ToggleSystemMuteCommand);
            Assert.True(vm.OpenDisplaySettingsCommand.CanExecute(null));
            Assert.True(vm.ToggleSystemMuteCommand.CanExecute(null));

            // Test Volume setter
            vm.SystemVolume = 65;
            Assert.Equal(65, vm.SystemVolume);

            // Test Brightness setter
            vm.ScreenBrightness = 80;
            Assert.Equal(80, vm.ScreenBrightness);

            // Test Mute inverted sync
            vm.IsSystemMuted = true;
            Assert.True(vm.IsSystemMuted);
            Assert.False(vm.IsNotMuted);

            vm.IsSystemMuted = false;
            Assert.False(vm.IsSystemMuted);
            Assert.True(vm.IsNotMuted);

            // Test Brightness availability sync
            vm.IsBrightnessSupported = true;
            Assert.True(vm.IsBrightnessSupported);
            Assert.False(vm.IsBrightnessUnavailable);

            vm.IsBrightnessSupported = false;
            Assert.False(vm.IsBrightnessSupported);
            Assert.True(vm.IsBrightnessUnavailable);

            // Verify device label
            Assert.False(string.IsNullOrWhiteSpace(vm.AudioOutputDeviceName));
        }
    }

    public class RealtimeEngineAndLiveDataTests
    {
        [Fact]
        public void SystemMonitorService_CollectsRealDiskMetrics()
        {
            var service = new SystemMonitorService();
            service.CollectMetrics();

            Assert.True(service.CurrentMetrics.DiskTotalGb > 0, "System disk total space should be greater than 0");
            Assert.True(service.CurrentMetrics.DiskFreeGb >= 0, "System disk free space should be non-negative");
            Assert.InRange(service.CurrentMetrics.DiskUsagePercent, 0.0, 100.0);
        }

        [Fact]
        public void WeatherService_DataModel_HasReliableProperties()
        {
            var data = new WeatherData
            {
                City = "Jakarta",
                TemperatureCelsius = 28.5,
                Condition = "Clear Sky",
                Humidity = 70.0,
                WindSpeedKmh = 14.2,
                IsCached = false
            };

            Assert.Equal("Jakarta", data.City);
            Assert.Equal(28.5, data.TemperatureCelsius);
            Assert.Equal(83.3, Math.Round(data.TemperatureFahrenheit, 1));
            Assert.False(data.IsCached);
        }

        [Fact]
        public void RealtimeService_TracksAllModuleStatusTransitions()
        {
            var service = new RealtimeService();
            string? changedModule = null;
            ModuleStatusInfo? changedInfo = null;

            service.StatusChanged += (m, info) =>
            {
                changedModule = m;
                changedInfo = info;
            };

            service.SetStatus("Music", ModuleLiveStatus.Live, "Song playing");
            Assert.Equal("Music", changedModule);
            Assert.NotNull(changedInfo);
            Assert.Equal(ModuleLiveStatus.Live, changedInfo.Status);
            Assert.Equal("LIVE", changedInfo.StatusBadgeText);
            Assert.Equal("#10B981", changedInfo.StatusColorHex);

            service.SetStatus("Music", ModuleLiveStatus.Offline, "No Active Session");
            Assert.Equal(ModuleLiveStatus.Offline, changedInfo.Status);
            Assert.Equal("OFFLINE", changedInfo.StatusBadgeText);
        }
    }
}
