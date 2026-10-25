using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NexIsland.Plugins;
using NexIsland.Services;
using NexIsland.Settings;

namespace NexIsland.ViewModels
{
    public enum IslandVisualState
    {
        Compact,
        Expanded,
        Notification
    }

    public enum IslandModule
    {
        Music,
        System,
        Focus,
        Lyrics,
        Battery,
        Network,
        Ai,
        Search,
        Routines,
        Launcher,
        Notifications,
        Weather,
        Calendar,
        Clipboard,
        Visualizer,
        Controls,
        Gaming,
        FileDrop,
        Activity,
        Themes,
        Plugins
    }

    // Retain ExpandedTab for backward compatibility
    public enum ExpandedTab
    {
        Music,
        System,
        Focus,
        Launcher,
        QuickControls,
        Clipboard,
        Weather,
        Calendar
    }

    public class DroppedFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string SizeString { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
    }

    public class MainViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService = SettingsService.Instance;
        private readonly MediaService _mediaService = MediaService.Instance;
        private readonly LyricsService _lyricsService = LyricsService.Instance;
        private readonly BatteryService _batteryService = BatteryService.Instance;
        private readonly SystemMonitorService _systemService = SystemMonitorService.Instance;
        private readonly NetworkService _networkService = NetworkService.Instance;
        private readonly FocusTimerService _focusService = FocusTimerService.Instance;
        private readonly LauncherService _launcherService = LauncherService.Instance;
        private readonly AudioVisualizerService _visualizerService = AudioVisualizerService.Instance;
        private readonly WeatherService _weatherService = WeatherService.Instance;
        private readonly ClipboardService _clipboardService = ClipboardService.Instance;
        private readonly CalendarService _calendarService = CalendarService.Instance;
        private readonly GamingModeService _gamingService = GamingModeService.Instance;
        private readonly NotificationService _notificationService = NotificationService.Instance;
        private readonly SmartActivityService _activityService = SmartActivityService.Instance;
        private readonly AiService _aiService = AiService.Instance;
        private readonly SearchService _searchService = SearchService.Instance;
        private readonly RoutineService _routineService = RoutineService.Instance;
        private readonly RealtimeService _realtimeService = RealtimeService.Instance;
        private readonly BrightnessService _brightnessService = BrightnessService.Instance;
        private readonly AudioControlService _audioControlService = AudioControlService.Instance;

        #region Island State & Dimensions
        private IslandVisualState _state = IslandVisualState.Compact;
        private IslandModule _currentModule = IslandModule.Music;
        private double _width;
        private double _height;
        private double _cornerRadius;
        private double _opacity;

        public IslandVisualState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    UpdateDimensionsForState();
                    OnPropertyChanged(nameof(IsCompact));
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(IsNotificationState));
                    _realtimeService.SetIslandState(IsExpanded, _currentModule.ToString());
                }
            }
        }

        public bool IsCompact => _state == IslandVisualState.Compact;
        public bool IsExpanded => _state == IslandVisualState.Expanded;
        public bool IsNotificationState => _state == IslandVisualState.Notification;

        public IslandModule CurrentModule
        {
            get => _currentModule;
            set
            {
                if (SetProperty(ref _currentModule, value))
                {
                    NotifyModuleChanged();
                }
            }
        }

        // Backward-compatible CurrentTab property
        public ExpandedTab CurrentTab
        {
            get => _currentModule switch
            {
                IslandModule.System => ExpandedTab.System,
                IslandModule.Focus => ExpandedTab.Focus,
                IslandModule.Launcher => ExpandedTab.Launcher,
                IslandModule.Controls => ExpandedTab.QuickControls,
                IslandModule.Clipboard => ExpandedTab.Clipboard,
                IslandModule.Weather => ExpandedTab.Weather,
                IslandModule.Calendar => ExpandedTab.Calendar,
                _ => ExpandedTab.Music
            };
            set
            {
                CurrentModule = value switch
                {
                    ExpandedTab.System => IslandModule.System,
                    ExpandedTab.Focus => IslandModule.Focus,
                    ExpandedTab.Launcher => IslandModule.Launcher,
                    ExpandedTab.QuickControls => IslandModule.Controls,
                    ExpandedTab.Clipboard => IslandModule.Clipboard,
                    ExpandedTab.Weather => IslandModule.Weather,
                    ExpandedTab.Calendar => IslandModule.Calendar,
                    _ => IslandModule.Music
                };
            }
        }

        private void NotifyModuleChanged()
        {
            OnPropertyChanged(nameof(IsMusicModule));
            OnPropertyChanged(nameof(IsLyricsModule));
            OnPropertyChanged(nameof(IsVisualizerModule));
            OnPropertyChanged(nameof(IsSystemModule));
            OnPropertyChanged(nameof(IsBatteryModule));
            OnPropertyChanged(nameof(IsNetworkModule));
            OnPropertyChanged(nameof(IsAiModule));
            OnPropertyChanged(nameof(IsSearchModule));
            OnPropertyChanged(nameof(IsRoutinesModule));
            OnPropertyChanged(nameof(IsLauncherModule));
            OnPropertyChanged(nameof(IsControlsModule));
            OnPropertyChanged(nameof(IsNotificationsModule));
            OnPropertyChanged(nameof(IsClipboardModule));
            OnPropertyChanged(nameof(IsWeatherModule));
            OnPropertyChanged(nameof(IsCalendarModule));
            OnPropertyChanged(nameof(IsFocusModule));
            OnPropertyChanged(nameof(IsActivityModule));
            OnPropertyChanged(nameof(IsGamingModule));
            OnPropertyChanged(nameof(IsFileDropModule));
            OnPropertyChanged(nameof(IsThemesModule));
            OnPropertyChanged(nameof(IsPluginsModule));

            // Legacy flags
            OnPropertyChanged(nameof(IsMusicTab));
            OnPropertyChanged(nameof(IsSystemTab));
            OnPropertyChanged(nameof(IsFocusTab));
            OnPropertyChanged(nameof(IsLauncherTab));
            OnPropertyChanged(nameof(IsControlsTab));
            OnPropertyChanged(nameof(IsClipboardTab));
            OnPropertyChanged(nameof(IsWeatherTab));
            OnPropertyChanged(nameof(IsCalendarTab));

            OnPropertyChanged(nameof(ActiveModuleName));
            OnPropertyChanged(nameof(ActiveModuleIndexText));
            OnPropertyChanged(nameof(ActiveModuleLiveBadge));
            OnPropertyChanged(nameof(ActiveModuleLiveColor));
            OnPropertyChanged(nameof(ActiveModuleLiveDetail));
            OnPropertyChanged(nameof(CurrentTab));

            _realtimeService.SetIslandState(IsExpanded, _currentModule.ToString());

            // Persist active module
            _settingsService.Current.LastActiveModule = _currentModule.ToString();
            _settingsService.Save();
        }

        public string ActiveModuleLiveBadge => _realtimeService.GetStatus(_currentModule.ToString()).StatusBadgeText;
        public string ActiveModuleLiveColor => _realtimeService.GetStatus(_currentModule.ToString()).StatusColorHex;
        public string ActiveModuleLiveDetail => _realtimeService.GetStatus(_currentModule.ToString()).Detail;

        public bool IsMusicModule => _currentModule == IslandModule.Music;
        public bool IsLyricsModule => _currentModule == IslandModule.Lyrics;
        public bool IsVisualizerModule => _currentModule == IslandModule.Visualizer;
        public bool IsSystemModule => _currentModule == IslandModule.System;
        public bool IsBatteryModule => _currentModule == IslandModule.Battery;
        public bool IsNetworkModule => _currentModule == IslandModule.Network;
        public bool IsAiModule => _currentModule == IslandModule.Ai;
        public bool IsSearchModule => _currentModule == IslandModule.Search;
        public bool IsRoutinesModule => _currentModule == IslandModule.Routines;
        public bool IsLauncherModule => _currentModule == IslandModule.Launcher;
        public bool IsControlsModule => _currentModule == IslandModule.Controls;
        public bool IsNotificationsModule => _currentModule == IslandModule.Notifications;
        public bool IsClipboardModule => _currentModule == IslandModule.Clipboard;
        public bool IsWeatherModule => _currentModule == IslandModule.Weather;
        public bool IsCalendarModule => _currentModule == IslandModule.Calendar;
        public bool IsFocusModule => _currentModule == IslandModule.Focus;
        public bool IsActivityModule => _currentModule == IslandModule.Activity;
        public bool IsGamingModule => _currentModule == IslandModule.Gaming;
        public bool IsFileDropModule => _currentModule == IslandModule.FileDrop;
        public bool IsThemesModule => _currentModule == IslandModule.Themes;
        public bool IsPluginsModule => _currentModule == IslandModule.Plugins;

        // Legacy tab flags
        public bool IsMusicTab => IsMusicModule;
        public bool IsSystemTab => IsSystemModule;
        public bool IsFocusTab => IsFocusModule;
        public bool IsLauncherTab => IsLauncherModule;
        public bool IsControlsTab => IsControlsModule;
        public bool IsClipboardTab => IsClipboardModule;
        public bool IsWeatherTab => IsWeatherModule;
        public bool IsCalendarTab => IsCalendarModule;

        public string ActiveModuleName => _currentModule switch
        {
            IslandModule.Music => "Music Player",
            IslandModule.Lyrics => "Live Lyrics",
            IslandModule.Visualizer => "Audio Visualizer",
            IslandModule.System => "System Monitor",
            IslandModule.Battery => "Smart Battery",
            IslandModule.Network => "Network Monitor",
            IslandModule.Ai => "Smart AI Island",
            IslandModule.Search => "Universal Search",
            IslandModule.Routines => "Smart Routines",
            IslandModule.Launcher => "Quick Launcher",
            IslandModule.Controls => "Quick Controls",
            IslandModule.Notifications => "Notification Center",
            IslandModule.Clipboard => "Clipboard History",
            IslandModule.Weather => "Weather",
            IslandModule.Calendar => "Agenda & Reminders",
            IslandModule.Focus => "Focus & Pomodoro",
            IslandModule.Activity => "Smart Activity",
            IslandModule.Gaming => "Gaming Mode",
            IslandModule.FileDrop => "File Drop Zone",
            IslandModule.Themes => "Themes & Preferences",
            IslandModule.Plugins => "Plugin System",
            _ => "NexIsland"
        };
        public string ActiveModuleIndexText
        {
            get
            {
                var list = GetEffectiveModuleList();
                int idx = list.IndexOf(_currentModule);
                if (idx < 0) idx = 0;
                return $"{idx + 1} / {list.Count}";
            }
        }

        public int ActiveModuleIndex
        {
            get
            {
                var list = GetEffectiveModuleList();
                int idx = list.IndexOf(_currentModule);
                return idx >= 0 ? idx : 0;
            }
            set
            {
                var list = GetEffectiveModuleList();
                if (value >= 0 && value < list.Count)
                {
                    CurrentModule = list[value];
                }
            }
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            set
            {
                if (SetProperty(ref _cornerRadius, value))
                {
                    OnPropertyChanged(nameof(IslandCornerRadius));
                }
            }
        }
        public CornerRadius IslandCornerRadius => new CornerRadius(_cornerRadius);

        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }
        #endregion

        #region Media State & Accurate Duration
        private string _songTitle = "No media playing";
        private string _artistName = "Ready for music";
        private bool _isPlaying;
        private double _mediaProgress;
        private BitmapImage? _albumArt;
        private bool _hasMediaSession;
        private bool _hasValidDuration;

        public string SongTitle { get => _songTitle; set => SetProperty(ref _songTitle, value); }
        public string ArtistName { get => _artistName; set => SetProperty(ref _artistName, value); }
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    OnPropertyChanged(nameof(IsNotPlaying));
                    OnPropertyChanged(nameof(PlayPauseTooltip));
                }
            }
        }
        public bool IsNotPlaying => !_isPlaying;
        public string PlayPauseTooltip => _isPlaying ? "Pause" : "Play";
        public double MediaProgress { get => _mediaProgress; set => SetProperty(ref _mediaProgress, value); }

        private string _mediaPositionText = "--:--";
        private string _mediaDurationText = "--:--";
        public string MediaPositionText { get => _mediaPositionText; set => SetProperty(ref _mediaPositionText, value); }
        public string MediaDurationText { get => _mediaDurationText; set => SetProperty(ref _mediaDurationText, value); }

        public BitmapImage? AlbumArt
        {
            get => _albumArt;
            set
            {
                if (SetProperty(ref _albumArt, value))
                {
                    OnPropertyChanged(nameof(HasAlbumArt));
                    OnPropertyChanged(nameof(HasNoAlbumArt));
                }
            }
        }
        public bool HasAlbumArt => _albumArt != null;
        public bool HasNoAlbumArt => _albumArt == null;
        public bool HasMediaSession { get => _hasMediaSession; set => SetProperty(ref _hasMediaSession, value); }
        public bool HasValidDuration { get => _hasValidDuration; set => SetProperty(ref _hasValidDuration, value); }

        private bool _isUserSeeking;
        public bool IsUserSeeking
        {
            get => _isUserSeeking;
            set => SetProperty(ref _isUserSeeking, value);
        }

        public void BeginSeek()
        {
            _isUserSeeking = true;
        }

        public void UpdateSeekPreview(double fraction)
        {
            fraction = Math.Clamp(fraction, 0.0, 1.0);
            MediaProgress = fraction;
            if (_mediaService.CurrentTrack.Duration > TimeSpan.Zero)
            {
                var previewPos = TimeSpan.FromTicks((long)(_mediaService.CurrentTrack.Duration.Ticks * fraction));
                MediaPositionText = FormatTime(previewPos, true);
            }
        }

        public async void EndSeek(double fraction)
        {
            fraction = Math.Clamp(fraction, 0.0, 1.0);
            MediaProgress = fraction;
            if (_mediaService.CurrentTrack.Duration > TimeSpan.Zero)
            {
                var previewPos = TimeSpan.FromTicks((long)(_mediaService.CurrentTrack.Duration.Ticks * fraction));
                MediaPositionText = FormatTime(previewPos, true);
            }
            _isUserSeeking = false;
            await _mediaService.SeekToFractionAsync(fraction);
        }
        #endregion

        #region Live Lyrics & Karaoke
        private string _currentLyricLine = "Ready for music";
        private string _previousLyricLine = string.Empty;
        private string _nextLyricLine = string.Empty;
        private bool _karaokeMode = true;

        public string CurrentLyricLine { get => _currentLyricLine; set => SetProperty(ref _currentLyricLine, value); }
        public string PreviousLyricLine { get => _previousLyricLine; set => SetProperty(ref _previousLyricLine, value); }
        public string NextLyricLine { get => _nextLyricLine; set => SetProperty(ref _nextLyricLine, value); }
        public bool KaraokeMode
        {
            get => _karaokeMode;
            set
            {
                if (SetProperty(ref _karaokeMode, value))
                {
                    _lyricsService.KaraokeMode = value;
                }
            }
        }
        public ObservableCollection<LyricLine> LyricsLines { get; } = new();
        #endregion

        #region System & Battery & Network
        private double _cpuUsage;
        private double _ramUsage;
        private string _ramText = "0 / 0 GB";
        private string _networkSpeedText = "0 KB/s";
        private int _batteryPercent = 100;
        private bool _isCharging;
        private bool _hasBattery;
        private string _batteryText = "100%";
        private string _connectionType = "Wi-Fi";
        private string _ipAddress = "192.168.1.100";

        private double _diskUsage;
        private string _diskText = "0 GB free";
        private string _batteryEstimatedTimeText = "AC Power";

        public double CpuUsage { get => _cpuUsage; set => SetProperty(ref _cpuUsage, value); }
        public double RamUsage { get => _ramUsage; set => SetProperty(ref _ramUsage, value); }
        public string RamText { get => _ramText; set => SetProperty(ref _ramText, value); }
        public double DiskUsage { get => _diskUsage; set => SetProperty(ref _diskUsage, value); }
        public string DiskText { get => _diskText; set => SetProperty(ref _diskText, value); }
        public string NetworkSpeedText { get => _networkSpeedText; set => SetProperty(ref _networkSpeedText, value); }
        public int BatteryPercent { get => _batteryPercent; set => SetProperty(ref _batteryPercent, value); }
        public bool IsCharging { get => _isCharging; set => SetProperty(ref _isCharging, value); }
        public bool HasBattery { get => _hasBattery; set => SetProperty(ref _hasBattery, value); }
        public string BatteryText { get => _batteryText; set => SetProperty(ref _batteryText, value); }
        public string BatteryEstimatedTimeText { get => _batteryEstimatedTimeText; set => SetProperty(ref _batteryEstimatedTimeText, value); }
        public string ConnectionType { get => _connectionType; set => SetProperty(ref _connectionType, value); }
        public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }
        #endregion

        #region Focus Timer
        private string _focusTimeFormatted = "25:00";
        private double _focusProgress;
        private bool _isFocusRunning;
        private string _focusStageText = "Work Session";
        private int _todayFocusMinutes;

        public string FocusTimeFormatted { get => _focusTimeFormatted; set => SetProperty(ref _focusTimeFormatted, value); }
        public double FocusProgress { get => _focusProgress; set => SetProperty(ref _focusProgress, value); }
        public bool IsFocusRunning { get => _isFocusRunning; set => SetProperty(ref _isFocusRunning, value); }
        public string FocusStageText { get => _focusStageText; set => SetProperty(ref _focusStageText, value); }
        public int TodayFocusMinutes { get => _todayFocusMinutes; set => SetProperty(ref _todayFocusMinutes, value); }
        #endregion

        #region Weather & Network Status
        private string _weatherTempText = "--°";
        private string _weatherCondition = "Loading...";
        private string _weatherEmoji = "☀️";
        private string _networkStatus = "Connected";
        private string _weatherHumidity = "65%";
        private string _weatherWind = "12 km/h";

        public string WeatherTempText { get => _weatherTempText; set => SetProperty(ref _weatherTempText, value); }
        public string WeatherCondition { get => _weatherCondition; set => SetProperty(ref _weatherCondition, value); }
        public string WeatherEmoji { get => _weatherEmoji; set => SetProperty(ref _weatherEmoji, value); }
        public string NetworkStatus { get => _networkStatus; set => SetProperty(ref _networkStatus, value); }
        public string WeatherHumidity { get => _weatherHumidity; set => SetProperty(ref _weatherHumidity, value); }
        public string WeatherWind { get => _weatherWind; set => SetProperty(ref _weatherWind, value); }
        private string _weatherCity = "Jakarta";
        private string _weatherLastUpdatedText = "Live API";
        public string WeatherCity { get => _weatherCity; set => SetProperty(ref _weatherCity, value); }
        public string WeatherLastUpdatedText { get => _weatherLastUpdatedText; set => SetProperty(ref _weatherLastUpdatedText, value); }
        #endregion

        #region Notification Preview & Center
        private string _notificationTitle = string.Empty;
        private string _notificationMessage = string.Empty;
        private string _notificationEmoji = "🔔";

        public string NotificationTitle { get => _notificationTitle; set => SetProperty(ref _notificationTitle, value); }
        public string NotificationMessage { get => _notificationMessage; set => SetProperty(ref _notificationMessage, value); }
        public string NotificationEmoji { get => _notificationEmoji; set => SetProperty(ref _notificationEmoji, value); }

        public ObservableCollection<IslandNotification> Notifications { get; } = new();
        #endregion

        #region Audio Visualizer
        public ObservableCollection<double> VisualizerBars { get; } = new();
        #endregion

        #region Launcher, Clipboard, Calendar & Activities
        private string _launcherQuery = string.Empty;
        public ObservableCollection<LauncherItem> FilteredLauncherItems { get; } = new();
        public ObservableCollection<ClipboardItem> ClipboardItems { get; } = new();
        public ObservableCollection<ReminderItem> ReminderItems { get; } = new();
        public ObservableCollection<ActivityTask> ActiveTasks { get; } = new();
        public ObservableCollection<DroppedFileInfo> DroppedFiles { get; } = new();
        public ObservableCollection<INexIslandPlugin> Plugins { get; } = new();

        public string LauncherQuery
        {
            get => _launcherQuery;
            set
            {
                if (SetProperty(ref _launcherQuery, value))
                {
                    RefreshLauncherList();
                }
            }
        }
        #endregion

        #region Gaming & Mode
        private bool _isGamingMode;
        private string _activeModeName = "Balanced";
        private string _gameProcessName = "None";

        public bool IsGamingMode { get => _isGamingMode; set => SetProperty(ref _isGamingMode, value); }
        public string ActiveModeName { get => _activeModeName; set => SetProperty(ref _activeModeName, value); }
        public string GameProcessName { get => _gameProcessName; set => SetProperty(ref _gameProcessName, value); }
        #endregion

        #region Integrated Themes & Preferences (In-Panel Settings)
        public bool StartWithWindows
        {
            get => _settingsService.Current.StartWithWindows;
            set
            {
                if (_settingsService.Current.StartWithWindows != value)
                {
                    _settingsService.Current.StartWithWindows = value;
                    StartupService.SetStartup(value);
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool AlwaysOnTop
        {
            get => _settingsService.Current.AlwaysOnTop;
            set
            {
                _settingsService.Current.AlwaysOnTop = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }

        public bool LockPosition
        {
            get => _settingsService.Current.LockPosition;
            set
            {
                _settingsService.Current.LockPosition = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }

        public bool ReducedMotion
        {
            get => _settingsService.Current.ReducedMotion;
            set
            {
                _settingsService.Current.ReducedMotion = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }

        public ThemeStyle CurrentTheme
        {
            get => _settingsService.Current.Theme;
            set
            {
                _settingsService.Current.Theme = value;
                ThemeService.Instance.ApplyTheme(_settingsService.Current);
                _settingsService.Save();
                OnPropertyChanged();
            }
        }

        public string AccentColorHex
        {
            get => _settingsService.Current.AccentColorHex;
            set
            {
                _settingsService.Current.AccentColorHex = value;
                ThemeService.Instance.ApplyTheme(_settingsService.Current);
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand ToggleExpandCommand { get; }
        public ICommand CollapseCommand { get; }
        public ICommand NextModuleCommand { get; }
        public ICommand PrevModuleCommand { get; }
        public ICommand SelectModuleCommand { get; }
        public ICommand SelectTabCommand { get; }

        public ICommand PlayPauseCommand { get; }
        public ICommand NextTrackCommand { get; }
        public ICommand PrevTrackCommand { get; }
        public ICommand SeekCommand { get; }

        public ICommand StartPomodoroCommand { get; }
        public ICommand PauseTimerCommand { get; }
        public ICommand ResetTimerCommand { get; }

        public ICommand LaunchItemCommand { get; }
        public ICommand CopyClipboardCommand { get; }
        public ICommand DeleteClipboardCommand { get; }
        public ICommand ClearClipboardCommand { get; }

        public ICommand CompleteReminderCommand { get; }
        public ICommand DismissNotificationCommand { get; }
        public ICommand ClearNotificationsCommand { get; }
        public ICommand CancelActivityCommand { get; }

        public ICommand QuickControlCommand { get; }
        public ICommand ApplyThemePresetCommand { get; }
        public ICommand OpenDroppedFileCommand { get; }
        public ICommand TogglePluginCommand { get; }

        public ICommand ResetModuleOrderCommand { get; }
        public ICommand MoveModuleUpCommand { get; }
        public ICommand MoveModuleDownCommand { get; }
        public ObservableCollection<string> ModuleOrderList { get; } = new();

        // Smart AI Commands & Properties
        public ObservableCollection<AiMessage> AiHistory => _aiService.ConversationHistory;
        private string _aiPrompt = string.Empty;
        public string AiPrompt { get => _aiPrompt; set => SetProperty(ref _aiPrompt, value); }
        public bool IsAiThinking => _aiService.IsThinking;
        public ICommand SendAiPromptCommand { get; }
        public ICommand ClearAiHistoryCommand { get; }
        public ICommand QuickAiActionCommand { get; }
        public ICommand SetAiApiKeyCommand { get; }

        // Universal Search Commands & Properties
        private string _universalSearchQuery = string.Empty;
        public string UniversalSearchQuery
        {
            get => _universalSearchQuery;
            set
            {
                if (SetProperty(ref _universalSearchQuery, value))
                {
                    _ = PerformUniversalSearchAsync(value);
                }
            }
        }
        public ObservableCollection<SearchResultItem> UniversalSearchResults { get; } = new();
        private SearchResultItem? _selectedUniversalResult;
        public SearchResultItem? SelectedUniversalResult
        {
            get => _selectedUniversalResult;
            set => SetProperty(ref _selectedUniversalResult, value);
        }
        private bool _isUniversalSearchOpen;
        public bool IsUniversalSearchOpen
        {
            get => _isUniversalSearchOpen;
            set => SetProperty(ref _isUniversalSearchOpen, value);
        }
        public ICommand OpenUniversalSearchCommand { get; }
        public ICommand CloseUniversalSearchCommand { get; }
        public ICommand ExecuteUniversalSearchResultCommand { get; }

        // Smart Routines Commands & Properties
        public ObservableCollection<SmartRoutine> SmartRoutines => _routineService.Routines;
        public ICommand RunRoutineCommand { get; }
        public ICommand ToggleRoutineCommand { get; }
        public ICommand DeleteRoutineCommand { get; }

        // Realtime Brightness & Sound Properties
        private int _screenBrightness = 50;
        public int ScreenBrightness
        {
            get => _screenBrightness;
            set
            {
                if (SetProperty(ref _screenBrightness, value))
                {
                    _brightnessService.SetBrightness(value);
                }
            }
        }

        private bool _isBrightnessSupported = true;
        public bool IsBrightnessSupported
        {
            get => _isBrightnessSupported;
            set
            {
                if (SetProperty(ref _isBrightnessSupported, value))
                {
                    OnPropertyChanged(nameof(IsBrightnessUnavailable));
                }
            }
        }
        public bool IsBrightnessUnavailable => !_isBrightnessSupported;
        public ICommand OpenDisplaySettingsCommand { get; }

        private int _systemVolume = 50;
        public int SystemVolume
        {
            get => _systemVolume;
            set
            {
                if (SetProperty(ref _systemVolume, value))
                {
                    _audioControlService.SetVolume(value);
                }
            }
        }

        private bool _isSystemMuted;
        public bool IsSystemMuted
        {
            get => _isSystemMuted;
            set
            {
                if (SetProperty(ref _isSystemMuted, value))
                {
                    OnPropertyChanged(nameof(IsNotMuted));
                }
            }
        }
        public bool IsNotMuted => !_isSystemMuted;
        public string AudioOutputDeviceName => _audioControlService.DeviceName;
        public ICommand ToggleSystemMuteCommand { get; }
        #endregion

        public MainViewModel()
        {
            var s = _settingsService.Current;
            _width = s.CompactWidth;
            _height = s.CompactHeight;
            _cornerRadius = s.CornerRadius;
            _opacity = s.OpacityLevel;

            if (Enum.TryParse<IslandModule>(s.LastActiveModule, out var savedMod))
            {
                _currentModule = savedMod;
            }

            UpdateDimensionsForState();

            // Initialize visualizer bar collection
            for (int i = 0; i < s.VisualizerBarsCount; i++)
            {
                VisualizerBars.Add(2.0);
            }

            // Commands
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
            CollapseCommand = new RelayCommand(() => State = IslandVisualState.Compact);

            NextModuleCommand = new RelayCommand(NextModule);
            PrevModuleCommand = new RelayCommand(PreviousModule);

            SelectModuleCommand = new RelayCommand(p =>
            {
                if (p is IslandModule mod) CurrentModule = mod;
                else if (p is string name && Enum.TryParse<IslandModule>(name, out var parsed)) CurrentModule = parsed;
            });

            SelectTabCommand = new RelayCommand(p =>
            {
                if (p is ExpandedTab tab) CurrentTab = tab;
                else if (p is string tabName && Enum.TryParse<ExpandedTab>(tabName, out var parsed)) CurrentTab = parsed;
                else if (p is IslandModule mod) CurrentModule = mod;
                else if (p is string name && Enum.TryParse<IslandModule>(name, out var parsedMod)) CurrentModule = parsedMod;
            });

            PlayPauseCommand = new AsyncRelayCommand(() => _mediaService.TogglePlayPauseAsync());
            NextTrackCommand = new AsyncRelayCommand(() => _mediaService.NextTrackAsync());
            PrevTrackCommand = new AsyncRelayCommand(() => _mediaService.PreviousTrackAsync());
            SeekCommand = new AsyncRelayCommand(p =>
            {
                if (p is double frac) return _mediaService.SeekToFractionAsync(frac);
                if (p is string sFrac && double.TryParse(sFrac, out var dFrac)) return _mediaService.SeekToFractionAsync(dFrac);
                return Task.CompletedTask;
            });

            StartPomodoroCommand = new RelayCommand(() =>
            {
                if (_focusService.IsRunning && _focusService.IsPaused) _focusService.Resume();
                else _focusService.StartPomodoro();
            });
            PauseTimerCommand = new RelayCommand(() => _focusService.Pause());
            ResetTimerCommand = new RelayCommand(() => _focusService.Reset());

            LaunchItemCommand = new RelayCommand(p =>
            {
                if (p is LauncherItem item)
                {
                    _launcherService.Launch(item);
                    State = IslandVisualState.Compact;
                }
            });

            CopyClipboardCommand = new RelayCommand(p =>
            {
                if (p is ClipboardItem item)
                {
                    _clipboardService.CopyToClipboard(item.Content);
                    _notificationService.PostNotification("Copied to Clipboard", item.PreviewText, "NexIsland Clipboard", "📋");
                }
            });
            DeleteClipboardCommand = new RelayCommand(p =>
            {
                if (p is ClipboardItem item) _clipboardService.DeleteItem(item.Id);
            });
            ClearClipboardCommand = new RelayCommand(() => _clipboardService.ClearHistory());

            CompleteReminderCommand = new RelayCommand(p =>
            {
                if (p is ReminderItem rem) _calendarService.CompleteReminder(rem.Id);
            });

            DismissNotificationCommand = new RelayCommand(p =>
            {
                if (p is IslandNotification notif) _notificationService.Dismiss(notif.Id);
            });
            ClearNotificationsCommand = new RelayCommand(() => _notificationService.ClearAll());

            CancelActivityCommand = new RelayCommand(p =>
            {
                if (p is ActivityTask task) _activityService.CancelTask(task.Id);
            });

            QuickControlCommand = new RelayCommand(p => ExecuteQuickControl(p?.ToString()));

            ApplyThemePresetCommand = new RelayCommand(p =>
            {
                if (p is ThemePreset preset)
                {
                    CurrentTheme = preset.Style;
                    AccentColorHex = preset.AccentHex;
                    _settingsService.Current.OpacityLevel = preset.Opacity;
                    ThemeService.Instance.ApplyTheme(_settingsService.Current);
                    _settingsService.Save();
                }
            });

            OpenDroppedFileCommand = new RelayCommand(p =>
            {
                if (p is DroppedFileInfo df && File.Exists(df.Path))
                {
                    try { Process.Start(new ProcessStartInfo { FileName = df.Path, UseShellExecute = true }); } catch { }
                }
            });

            TogglePluginCommand = new AsyncRelayCommand(p =>
            {
                if (p is INexIslandPlugin plugin)
                {
                    return PluginService.Instance.TogglePluginAsync(plugin.Metadata.Id, !plugin.IsEnabled);
                }
                return Task.CompletedTask;
            });

            // Smart AI commands
            SendAiPromptCommand = new AsyncRelayCommand(async () =>
            {
                if (string.IsNullOrWhiteSpace(AiPrompt)) return;
                string prompt = AiPrompt;
                AiPrompt = string.Empty;
                await _aiService.AskAsync(prompt);
            });

            ClearAiHistoryCommand = new RelayCommand(() => _aiService.ClearHistory());

            QuickAiActionCommand = new AsyncRelayCommand(p =>
            {
                if (p is string action)
                {
                    string prompt = action switch
                    {
                        "summarize" => $"Summarize the following text clearly with key bullet points: {_clipboardService.History.FirstOrDefault()?.Content ?? "NexIsland Dynamic Island on Windows 10/11"}",
                        "checklist" => "Create a practical 4-step focus checklist for my upcoming work session",
                        "plan" => "Create an optimized 2-hour daily study and deep work plan using Pomodoro intervals",
                        "tips" => "Show top 5 essential Windows 10/11 power shortcuts and productive tips",
                        "routine" => "Suggest a smart automation routine for deep work mode",
                        _ => action
                    };
                    return _aiService.AskAsync(prompt);
                }
                return Task.CompletedTask;
            });

            SetAiApiKeyCommand = new RelayCommand(p =>
            {
                if (p is string key)
                {
                    _aiService.SetApiKey(key);
                    NotificationService.Instance.PostNotification("AI Configuration", "API key updated and securely encrypted in local vault.", "NexIsland AI", "🤖");
                }
            });

            // Universal Search commands
            OpenUniversalSearchCommand = new RelayCommand(OpenUniversalSearch);
            CloseUniversalSearchCommand = new RelayCommand(CloseUniversalSearch);
            ExecuteUniversalSearchResultCommand = new RelayCommand(p =>
            {
                if (p is SearchResultItem item)
                {
                    _searchService.ExecuteResult(item, modName =>
                    {
                        if (Enum.TryParse<IslandModule>(modName, out var mod))
                        {
                            CurrentModule = mod;
                            State = IslandVisualState.Expanded;
                        }
                    });
                    CloseUniversalSearch();
                }
            });

            // Smart Routines commands
            RunRoutineCommand = new AsyncRelayCommand(p =>
            {
                if (p is SmartRoutine routine)
                {
                    return _routineService.ExecuteRoutineAsync(routine, "User Triggered", modName =>
                    {
                        if (Enum.TryParse<IslandModule>(modName, out var mod))
                        {
                            CurrentModule = mod;
                            State = IslandVisualState.Expanded;
                        }
                    });
                }
                return Task.CompletedTask;
            });

            ToggleRoutineCommand = new RelayCommand(p =>
            {
                if (p is SmartRoutine routine)
                {
                    routine.IsEnabled = !routine.IsEnabled;
                    _routineService.SaveRoutines();
                    OnPropertyChanged(nameof(SmartRoutines));
                }
            });

            DeleteRoutineCommand = new RelayCommand(p =>
            {
                if (p is SmartRoutine routine)
                {
                    _routineService.DeleteRoutine(routine.Id);
                    OnPropertyChanged(nameof(SmartRoutines));
                }
            });

            ResetModuleOrderCommand = new RelayCommand(() =>
            {
                _settingsService.Current.ModuleOrder = new List<string>
                {
                    "Music", "System", "Focus", "Lyrics", "Battery", "Network",
                    "Ai", "Search", "Routines", "Launcher", "Notifications", "Weather",
                    "Calendar", "Clipboard", "Visualizer", "Controls", "Gaming",
                    "FileDrop", "Activity", "Themes", "Plugins"
                };
                _settingsService.Save();
                RefreshModuleOrderList();
                NotifyModuleChanged();
            });

            MoveModuleUpCommand = new RelayCommand(p =>
            {
                if (p is string modName)
                {
                    var list = _settingsService.Current.ModuleOrder;
                    int idx = list.IndexOf(modName);
                    if (idx > 0)
                    {
                        list.RemoveAt(idx);
                        list.Insert(idx - 1, modName);
                        _settingsService.Save();
                        RefreshModuleOrderList();
                        NotifyModuleChanged();
                    }
                }
            });

            MoveModuleDownCommand = new RelayCommand(p =>
            {
                if (p is string modName)
                {
                    var list = _settingsService.Current.ModuleOrder;
                    int idx = list.IndexOf(modName);
                    if (idx >= 0 && idx < list.Count - 1)
                    {
                        list.RemoveAt(idx);
                        list.Insert(idx + 1, modName);
                        _settingsService.Save();
                        RefreshModuleOrderList();
                        NotifyModuleChanged();
                    }
                }
            });

            OpenDisplaySettingsCommand = new RelayCommand(() => _brightnessService.OpenDisplaySettings());
            ToggleSystemMuteCommand = new RelayCommand(() => _audioControlService.ToggleMute());
            _screenBrightness = _brightnessService.CurrentBrightness;
            _isBrightnessSupported = _brightnessService.IsSupported;
            _systemVolume = _audioControlService.MasterVolume;
            _isSystemMuted = _audioControlService.IsMuted;

            RefreshModuleOrderList();
            SubscribeToServices();
        }

        public void OpenUniversalSearch()
        {
            IsUniversalSearchOpen = true;
            UniversalSearchQuery = string.Empty;
            _ = PerformUniversalSearchAsync(string.Empty);
        }

        public void CloseUniversalSearch()
        {
            IsUniversalSearchOpen = false;
        }

        public async Task PerformUniversalSearchAsync(string query)
        {
            var results = await _searchService.SearchAsync(query);
            UniversalSearchResults.Clear();
            foreach (var r in results)
            {
                UniversalSearchResults.Add(r);
            }
            SelectedUniversalResult = UniversalSearchResults.FirstOrDefault();
        }

        private void RefreshModuleOrderList()
        {
            ModuleOrderList.Clear();
            var list = GetEffectiveModuleList();
            foreach (var mod in list)
            {
                ModuleOrderList.Add(mod.ToString());
            }
        }

        public List<IslandModule> GetEffectiveModuleList()
        {
            var result = new List<IslandModule>();
            var s = _settingsService.Current;
            if (s.ModuleOrder != null && s.ModuleOrder.Count > 0)
            {
                foreach (var name in s.ModuleOrder)
                {
                    if (Enum.TryParse<IslandModule>(name, out var mod))
                    {
                        if (!result.Contains(mod))
                        {
                            result.Add(mod);
                        }
                    }
                }
            }

            foreach (IslandModule mod in Enum.GetValues<IslandModule>())
            {
                if (!result.Contains(mod))
                {
                    result.Add(mod);
                }
            }

            return result;
        }

        public void NextModule()
        {
            var list = GetEffectiveModuleList();
            if (list.Count == 0) return;
            int idx = list.IndexOf(_currentModule);
            if (idx < 0) idx = 0;
            int next = (idx + 1) % list.Count;
            CurrentModule = list[next];
        }

        public void PreviousModule()
        {
            var list = GetEffectiveModuleList();
            if (list.Count == 0) return;
            int idx = list.IndexOf(_currentModule);
            if (idx < 0) idx = 0;
            int prev = (idx - 1 + list.Count) % list.Count;
            CurrentModule = list[prev];
        }

        public async Task InitializeServicesAsync(Window window)
        {
            _clipboardService.Initialize(window);
            HotKeyService.Instance.Initialize(window);
            HotKeyService.Instance.SearchHotKeyPressed += () => Application.Current?.Dispatcher.Invoke(OpenUniversalSearch);
            _aiService.ThinkingStateChanged += _ => OnPropertyChanged(nameof(IsAiThinking));
            _routineService.RoutinesChanged += () => OnPropertyChanged(nameof(SmartRoutines));

            await _mediaService.InitializeAsync();
            _batteryService.Start();
            _systemService.Start();
            await _networkService.StartAsync();
            _visualizerService.Start();
            _weatherService.Start();
            _gamingService.Start();
            await PluginService.Instance.InitializeAllAsync();

            RefreshLauncherList();
            RefreshClipboardList();
            RefreshReminderList();
            RefreshNotificationList();
            RefreshActivityList();
            RefreshPluginList();
            _ = PerformUniversalSearchAsync(string.Empty);
        }

        private void SubscribeToServices()
        {
            // Media track change
            _mediaService.TrackUpdated += track =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    HasMediaSession = track.HasActiveSession;
                    SongTitle = track.Title;
                    ArtistName = track.Artist;
                    IsPlaying = track.IsPlaying;
                    HasValidDuration = track.HasValidDuration;
                    MediaProgress = track.ProgressFraction;
                    MediaPositionText = FormatTime(track.Position, track.HasValidDuration);
                    MediaDurationText = FormatDuration(track.Duration);
                    AlbumArt = track.Thumbnail;
                });
            };

            // High-precision position tick
            _mediaService.PositionTick += (pos, dur, frac) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (!_isUserSeeking)
                    {
                        MediaProgress = frac;
                        MediaPositionText = FormatTime(pos, dur > TimeSpan.Zero);
                        MediaDurationText = FormatDuration(dur);
                    }
                });
            };

            // Lyrics
            _lyricsService.ActiveLineChanged += line =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentLyricLine = line;
                    PreviousLyricLine = _lyricsService.PreviousLineText;
                    NextLyricLine = _lyricsService.NextLineText;
                });
            };

            _lyricsService.LyricsUpdated += () =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    LyricsLines.Clear();
                    foreach (var l in _lyricsService.CurrentLyrics)
                    {
                        LyricsLines.Add(l);
                    }
                    CurrentLyricLine = _lyricsService.CurrentLineText;
                    PreviousLyricLine = _lyricsService.PreviousLineText;
                    NextLyricLine = _lyricsService.NextLineText;
                });
            };

            // Battery
            _batteryService.BatteryUpdated += bat =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    HasBattery = bat.HasBattery;
                    BatteryPercent = bat.Percentage;
                    IsCharging = bat.IsCharging;
                    BatteryText = bat.StatusSummary;
                    BatteryEstimatedTimeText = bat.EstimatedTimeRemaining.HasValue
                        ? $"{bat.EstimatedTimeRemaining.Value.Hours}h {bat.EstimatedTimeRemaining.Value.Minutes:D2}m remaining"
                        : (bat.IsCharging ? "Charging" : (bat.HasBattery ? "Calculating..." : "AC Connected"));
                });
            };
            _batteryService.BatteryAlert += msg => ShowTempNotification("Battery Alert", msg, "🔋");

            // System Monitor
            _systemService.MetricsUpdated += m =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CpuUsage = m.CpuUsagePercent;
                    RamUsage = m.RamUsagePercent;
                    RamText = $"{m.RamUsedGb:F1} / {m.RamTotalGb:F1} GB";
                    DiskUsage = m.DiskUsagePercent;
                    DiskText = $"{m.DiskFreeGb:F0} GB free of {m.DiskTotalGb:F0} GB";
                    NetworkSpeedText = $"↓ {m.NetworkDownloadKbps:F0} KB/s  ↑ {m.NetworkUploadKbps:F0} KB/s";
                });
            };

            // Network
            _networkService.NetworkStatusChanged += net =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    NetworkStatus = net.StatusMessage;
                    ConnectionType = net.ConnectionType;
                });
            };
            _networkService.NetworkAlert += msg => ShowTempNotification("Network Status", msg, "🌐");

            // Audio Visualizer
            _visualizerService.BarsUpdated += bars =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < Math.Min(bars.Length, VisualizerBars.Count); i++)
                    {
                        VisualizerBars[i] = Math.Max(2.0, bars[i] * 22.0);
                    }
                });
            };

            // Focus Timer
            _focusService.Tick += () =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var rem = _focusService.RemainingTime;
                    FocusTimeFormatted = $"{rem.Minutes:D2}:{rem.Seconds:D2}";
                    FocusProgress = _focusService.ProgressFraction;
                    IsFocusRunning = _focusService.IsRunning && !_focusService.IsPaused;
                    FocusStageText = _focusService.CurrentPomodoroStage == PomodoroStage.Work ? "Focus Session" : "Break Time";
                    TodayFocusMinutes = _focusService.GetTodayFocusMinutes();
                });
            };
            _focusService.SessionCompleted += msg => ShowTempNotification("Focus Session", msg, "⏳");

            // Weather
            _weatherService.WeatherUpdated += w =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    WeatherTempText = $"{w.TemperatureCelsius:F0}°C";
                    WeatherCondition = w.Condition;
                    WeatherEmoji = w.WeatherEmoji;
                    WeatherHumidity = $"{w.Humidity:F0}%";
                    WeatherWind = $"{w.WindSpeedKmh:F0} km/h";
                    WeatherCity = w.City;
                    WeatherLastUpdatedText = w.IsCached
                        ? $"Cached: {w.LastUpdated:HH:mm}"
                        : $"Updated: {w.LastUpdated:HH:mm} (Live)";
                });
            };

            // Notifications
            _notificationService.NotificationReceived += notif =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RefreshNotificationList();
                    ShowTempNotification(notif.Title, notif.Message, notif.IconEmoji);
                });
            };
            _notificationService.HistoryChanged += () => Application.Current?.Dispatcher.Invoke(RefreshNotificationList);

            // Gaming mode
            _gamingService.GamingModeChanged += (active, proc) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsGamingMode = active;
                    GameProcessName = string.IsNullOrWhiteSpace(proc) ? "None" : proc;
                    if (active)
                    {
                        State = IslandVisualState.Compact;
                        _systemService.SetInterval(TimeSpan.FromSeconds(3)); // Eco mode
                    }
                    else
                    {
                        _systemService.SetInterval(TimeSpan.FromSeconds(1));
                    }
                });
            };

            // Smart Activity
            _activityService.TasksChanged += () => Application.Current?.Dispatcher.Invoke(RefreshActivityList);

            // Clipboard
            _clipboardService.HistoryChanged += () => Application.Current?.Dispatcher.Invoke(RefreshClipboardList);

            // Reminders
            _calendarService.RemindersChanged += () => Application.Current?.Dispatcher.Invoke(RefreshReminderList);
            _calendarService.ReminderDue += rem => ShowTempNotification("Reminder Due", rem.Title, "📅");

            // Plugins
            PluginService.Instance.PluginsUpdated += () => Application.Current?.Dispatcher.Invoke(RefreshPluginList);

            // Realtime Service Engine
            _realtimeService.StatusChanged += (mod, status) =>
            {
                if (mod.Equals(_currentModule.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(ActiveModuleLiveBadge));
                        OnPropertyChanged(nameof(ActiveModuleLiveColor));
                        OnPropertyChanged(nameof(ActiveModuleLiveDetail));
                    });
                }
            };

            // Brightness Service
            _brightnessService.BrightnessChanged += b =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _screenBrightness = b;
                    OnPropertyChanged(nameof(ScreenBrightness));
                });
            };
            _brightnessService.AvailabilityChanged += supported =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _isBrightnessSupported = supported;
                    OnPropertyChanged(nameof(IsBrightnessSupported));
                    OnPropertyChanged(nameof(IsBrightnessUnavailable));
                });
            };

            // Audio Control Service
            _audioControlService.VolumeChanged += (vol, isMuted) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _systemVolume = vol;
                    _isSystemMuted = isMuted;
                    OnPropertyChanged(nameof(SystemVolume));
                    OnPropertyChanged(nameof(IsSystemMuted));
                    OnPropertyChanged(nameof(IsNotMuted));
                });
            };
            _audioControlService.DeviceChanged += _ =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(AudioOutputDeviceName));
                });
            };
        }

        private static string FormatTime(TimeSpan time, bool hasDuration)
        {
            if (!hasDuration && time == TimeSpan.Zero) return "--:--";
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
                : $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private static string FormatDuration(TimeSpan time)
        {
            if (time <= TimeSpan.Zero) return "--:--";
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
                : $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private void ToggleExpand()
        {
            State = (State == IslandVisualState.Compact) ? IslandVisualState.Expanded : IslandVisualState.Compact;
        }

        private void UpdateDimensionsForState()
        {
            var s = _settingsService.Current;
            switch (_state)
            {
                case IslandVisualState.Compact:
                    Width = s.CompactWidth;
                    Height = s.CompactHeight;
                    CornerRadius = s.CornerRadius;
                    Opacity = s.OpacityLevel;
                    break;
                case IslandVisualState.Expanded:
                    Width = s.ExpandedWidth;
                    Height = s.ExpandedHeight;
                    CornerRadius = 20;
                    Opacity = 0.98;
                    break;
                case IslandVisualState.Notification:
                    Width = 320;
                    Height = 52;
                    CornerRadius = 18;
                    Opacity = 1.0;
                    break;
            }
        }

        private void ShowTempNotification(string title, string message, string emoji)
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                NotificationTitle = title;
                NotificationMessage = message;
                NotificationEmoji = emoji;

                if (State == IslandVisualState.Compact)
                {
                    State = IslandVisualState.Notification;
                    await Task.Delay(4000);
                    if (State == IslandVisualState.Notification)
                    {
                        State = IslandVisualState.Compact;
                    }
                }
            });
        }

        public void AddDroppedFiles(string[] filePaths)
        {
            DroppedFiles.Clear();
            foreach (var path in filePaths)
            {
                try
                {
                    var fi = new FileInfo(path);
                    string size = fi.Exists ? $"{fi.Length / 1024.0:F1} KB" : "Directory";
                    DroppedFiles.Add(new DroppedFileInfo
                    {
                        Name = fi.Name,
                        Path = path,
                        SizeString = size,
                        Extension = fi.Extension.ToUpperInvariant()
                    });
                    LauncherService.Instance.AddItem(fi.Name, path, "Dropped Files", isPinned: true);
                }
                catch { }
            }
            CurrentModule = IslandModule.FileDrop;
            State = IslandVisualState.Expanded;
        }

        private void RefreshLauncherList()
        {
            FilteredLauncherItems.Clear();
            foreach (var item in _launcherService.Search(_launcherQuery))
            {
                FilteredLauncherItems.Add(item);
            }
        }

        private void RefreshClipboardList()
        {
            ClipboardItems.Clear();
            foreach (var item in _clipboardService.History)
            {
                ClipboardItems.Add(item);
            }
        }

        private void RefreshReminderList()
        {
            ReminderItems.Clear();
            foreach (var item in _calendarService.Reminders)
            {
                ReminderItems.Add(item);
            }
        }

        private void RefreshNotificationList()
        {
            Notifications.Clear();
            foreach (var notif in _notificationService.History)
            {
                Notifications.Add(notif);
            }
        }

        private void RefreshActivityList()
        {
            ActiveTasks.Clear();
            foreach (var t in _activityService.ActiveTasks)
            {
                ActiveTasks.Add(t);
            }
        }

        private void RefreshPluginList()
        {
            Plugins.Clear();
            foreach (var p in PluginService.Instance.Plugins)
            {
                Plugins.Add(p);
            }
        }

        private void ExecuteQuickControl(string? action)
        {
            try
            {
                switch (action)
                {
                    case "display":
                        ShowTempNotification("Display Control", $"{SystemParameters.PrimaryScreenWidth}×{SystemParameters.PrimaryScreenHeight} Monitor Active", "🖥️");
                        break;
                    case "sound":
                        ShowTempNotification("Sound Control", "Volume & Output Controls Ready", "🔊");
                        break;
                    case "wifi":
                        ShowTempNotification("Wi-Fi Connection", $"Status: {_networkStatus}", "📶");
                        break;
                    case "bluetooth":
                        ShowTempNotification("Bluetooth", "Bluetooth devices ready to connect", "ᛒ");
                        break;
                    case "battery":
                        ShowTempNotification("Battery", $"{_batteryPercent}% • {_batteryText}", "🔋");
                        break;
                    case "nightlight":
                        ShowTempNotification("Night Light", "Night lighting temperature adjusted", "🌙");
                        break;
                    case "mode_eco":
                        _activeModeName = "Eco";
                        _systemService.SetInterval(TimeSpan.FromSeconds(3));
                        OnPropertyChanged(nameof(ActiveModeName));
                        ShowTempNotification("Mode Changed", "Eco Mode activated (Low resource consumption)", "🌱");
                        break;
                    case "mode_balanced":
                        _activeModeName = "Balanced";
                        _systemService.SetInterval(TimeSpan.FromSeconds(1));
                        OnPropertyChanged(nameof(ActiveModeName));
                        ShowTempNotification("Mode Changed", "Balanced Mode activated", "⚖️");
                        break;
                    case "mode_performance":
                        _activeModeName = "Turbo";
                        _systemService.SetInterval(TimeSpan.FromMilliseconds(500));
                        OnPropertyChanged(nameof(ActiveModeName));
                        ShowTempNotification("Mode Changed", "Turbo Performance Mode activated", "⚡");
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Error executing quick control {action}", ex);
            }
        }
    }
}
