using System;
using System.Collections.Generic;

namespace NexIsland.Settings
{
    public enum ResourceActivityMode
    {
        Eco,
        Balanced,
        Performance
    }

    public enum ThemeStyle
    {
        Dark,
        Light,
        Translucent,
        CustomAccent
    }

    public class AppSettings
    {
        // General
        public bool StartWithWindows { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = true;
        public bool AutoHide { get; set; } = false;
        public bool LockPosition { get; set; } = true;
        public int SelectedMonitorIndex { get; set; } = 0;
        public double XOffset { get; set; } = 0;
        public double YOffset { get; set; } = 12;

        // Swipe Navigation & Modules
        public string LastActiveModule { get; set; } = "Music";
        public double SwipeThreshold { get; set; } = 40.0;
        public bool EnableTouchGestures { get; set; } = true;
        public bool EnableMouseWheelNavigation { get; set; } = true;

        public List<string> ModuleOrder { get; set; } = new()
        {
            "Music",
            "System",
            "Focus",
            "Lyrics",
            "Battery",
            "Network",
            "Ai",
            "Search",
            "Routines",
            "Launcher",
            "Notifications",
            "Weather",
            "Calendar",
            "Clipboard",
            "Visualizer",
            "Controls",
            "Gaming",
            "FileDrop",
            "Activity",
            "Themes",
            "Plugins"
        };

        public bool AdaptiveIslandMode { get; set; } = true;
        public bool EnableAiIsland { get; set; } = true;
        public bool EnableUniversalSearch { get; set; } = true;
        public bool EnableSmartRoutines { get; set; } = true;

        // Resource & Performance
        public ResourceActivityMode ActivityMode { get; set; } = ResourceActivityMode.Balanced;
        public bool ReducedMotion { get; set; } = false;
        public int PollingIntervalMs { get; set; } = 1000;

        // Appearance & Customization
        public ThemeStyle Theme { get; set; } = ThemeStyle.Dark;
        public string AccentColorHex { get; set; } = "#3B82F6"; // Elegant modern electric blue
        public double CompactWidth { get; set; } = 220;
        public double CompactHeight { get; set; } = 34;
        public double ExpandedWidth { get; set; } = 390;
        public double ExpandedHeight { get; set; } = 240;
        public double CornerRadius { get; set; } = 17;
        public double OpacityLevel { get; set; } = 0.98;
        public double AnimationDurationSeconds { get; set; } = 0.35;

        // Modules Enabled
        public bool EnableMedia { get; set; } = true;
        public bool EnableLyrics { get; set; } = true;
        public bool EnableVisualizer { get; set; } = true;
        public bool EnableSystemMonitor { get; set; } = true;
        public bool EnableBattery { get; set; } = true;
        public bool EnableNetwork { get; set; } = true;
        public bool EnableFocusTimer { get; set; } = true;
        public bool EnableLauncher { get; set; } = true;
        public bool EnableControls { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public bool EnableClipboard { get; set; } = true;
        public bool EnableWeather { get; set; } = true;
        public bool EnableGamingMode { get; set; } = true;
        public bool EnableCalendar { get; set; } = true;
        public bool EnableActivity { get; set; } = true;
        public bool EnableFileDropZone { get; set; } = true;
        public bool EnableThemes { get; set; } = true;
        public bool EnablePlugins { get; set; } = true;

        // Audio Visualizer
        public int VisualizerBarsCount { get; set; } = 12;
        public double VisualizerSensitivity { get; set; } = 1.2;

        // Focus & Pomodoro
        public int PomodoroWorkMinutes { get; set; } = 25;
        public int PomodoroShortBreakMinutes { get; set; } = 5;
        public int PomodoroLongBreakMinutes { get; set; } = 15;
        public int DailyFocusGoalMinutes { get; set; } = 120;

        // Weather
        public string WeatherCity { get; set; } = "Jakarta";
        public double WeatherLatitude { get; set; } = -6.2088;
        public double WeatherLongitude { get; set; } = 106.8456;
        public bool UseCelsius { get; set; } = true;
        public int WeatherRefreshMinutes { get; set; } = 30;

        // Clipboard
        public int MaxClipboardHistory { get; set; } = 30;
        public bool ClipboardPrivacyMode { get; set; } = false;

        // Gaming Mode
        public bool AutoDetectGames { get; set; } = true;
        public List<string> KnownGameProcesses { get; set; } = new()
        {
            "steam", "epicgameslauncher", "valorant", "cs2", "dota2", "overwatch", "genshinimpact", "fortnite"
        };
    }
}
