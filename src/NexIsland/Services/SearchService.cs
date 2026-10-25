using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexIsland.Services
{
    public enum SearchResultType
    {
        Application,
        NexIslandModule,
        WindowsSetting,
        Calculator,
        FileOrFolder,
        QuickAction
    }

    public class SearchResultItem
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public SearchResultType ResultType { get; set; }
        public string IconGeometryKey { get; set; } = "GeoApps";
        public string LaunchTarget { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public Action? CustomAction { get; set; }

        public string TypeBadge => ResultType switch
        {
            SearchResultType.Application => "APP",
            SearchResultType.NexIslandModule => "ISLAND",
            SearchResultType.WindowsSetting => "SETTING",
            SearchResultType.Calculator => "CALC",
            SearchResultType.FileOrFolder => "FILE",
            _ => "ACTION"
        };
    }

    public class SearchService
    {
        private static SearchService? _instance;
        public static SearchService Instance => _instance ??= new SearchService();

        private readonly List<SearchResultItem> _predefinedItems = new();
        private readonly List<SearchResultItem> _indexedApps = new();
        private bool _isAppsIndexed = false;

        public SearchService()
        {
            BuildPredefinedCatalog();
            Task.Run(IndexSystemApps);
        }

        private void BuildPredefinedCatalog()
        {
            // NexIsland Modules
            AddModuleItem("Music Player", "Switch to media playback & audio controls", "Music", "GeoMusic");
            AddModuleItem("Quick Controls", "Brightness, volume & system power controls", "Controls", "GeoControls");
            AddModuleItem("Live Lyrics & Karaoke", "Synchronized real-time track lyrics", "Lyrics", "GeoLyrics");
            AddModuleItem("Audio Visualizer", "Hardware WASAPI audio equalizer", "Visualizer", "GeoVisualizer");
            AddModuleItem("System Monitor", "Real-time CPU, RAM and Network metrics", "System", "GeoCpu");
            AddModuleItem("Smart Battery", "Battery percentage and power options", "Battery", "GeoBattery");
            AddModuleItem("Network Monitor", "Wi-Fi & Ethernet bandwidth throughput", "Network", "GeoWifi");
            AddModuleItem("Universal Search", "Fast search across apps, files & settings", "Search", "GeoSearch");
            AddModuleItem("Smart Routines", "Automate Study, Gaming & Battery Saver modes", "Routines", "GeoControls");
            AddModuleItem("Quick Launcher", "Fast access to pinned shortcuts & apps", "Launcher", "GeoApps");
            AddModuleItem("Notification Center", "Manage Windows & NexIsland notifications", "Notifications", "GeoBell");
            AddModuleItem("Clipboard History", "Search, pin and copy clipboard items", "Clipboard", "GeoClip");
            AddModuleItem("Weather", "Current weather conditions & forecasts", "Weather", "GeoWeather");
            AddModuleItem("Agenda & Reminders", "Calendar reminders and schedule", "Calendar", "GeoCalendar");
            AddModuleItem("Focus & Pomodoro", "25m focus timer & productivity tracker", "Focus", "GeoFocus");
            AddModuleItem("Smart Activity Island", "Multi-tasking background operations", "Activity", "GeoActivity");
            AddModuleItem("Gaming Mode", "Low polling profile for zero FPS drop", "Gaming", "GeoGame");
            AddModuleItem("File Drop Zone", "Direct file drag-and-drop workspace", "FileDrop", "GeoFileDrop");
            AddModuleItem("Themes & Island Setup", "Personalize accents, presets & module order", "Themes", "GeoPalette");
            AddModuleItem("Plugin System", "Manage NexIsland third-party extensions", "Plugins", "GeoPlugin");

            // Windows System Settings
            AddSettingItem("Display Settings", "ms-settings:display", "Change screen resolution and scale");
            AddSettingItem("Sound Settings", "ms-settings:sound", "Audio output device and master volume");
            AddSettingItem("Wi-Fi & Network Settings", "ms-settings:network-wifi", "Wireless network connections");
            AddSettingItem("Bluetooth & Devices", "ms-settings:bluetooth", "Manage connected accessories");
            AddSettingItem("Battery & Power Settings", "ms-settings:powersleep", "Sleep intervals and energy mode");
            AddSettingItem("Windows Update", "ms-settings:windowsupdate", "Check for OS patches and security");
            AddSettingItem("Task Manager", "taskmgr.exe", "View active processes and performance");
            AddSettingItem("Snipping Tool", "ms-screenclip:", "Take a quick screenshot (Win + Shift + S)");
        }

        private void AddModuleItem(string title, string subtitle, string moduleName, string iconKey)
        {
            _predefinedItems.Add(new SearchResultItem
            {
                Title = title,
                Subtitle = subtitle,
                ResultType = SearchResultType.NexIslandModule,
                IconGeometryKey = iconKey,
                LaunchTarget = moduleName
            });
        }

        private void AddSettingItem(string title, string targetUri, string subtitle)
        {
            _predefinedItems.Add(new SearchResultItem
            {
                Title = title,
                Subtitle = subtitle,
                ResultType = SearchResultType.WindowsSetting,
                IconGeometryKey = "GeoSettings",
                LaunchTarget = targetUri
            });
        }

        private void IndexSystemApps()
        {
            if (_isAppsIndexed) return;

            try
            {
                var searchDirs = new List<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System))
                };

                // Standard built-in system tools
                var systemTools = new (string Name, string Exe)[]
                {
                    ("Notepad", "notepad.exe"),
                    ("Calculator", "calc.exe"),
                    ("Command Prompt", "cmd.exe"),
                    ("PowerShell", "powershell.exe"),
                    ("File Explorer", "explorer.exe"),
                    ("Registry Editor", "regedit.exe"),
                    ("Device Manager", "devmgmt.msc"),
                    ("Disk Management", "diskmgmt.msc"),
                    ("Paint", "mspaint.exe")
                };

                foreach (var tool in systemTools)
                {
                    _indexedApps.Add(new SearchResultItem
                    {
                        Title = tool.Name,
                        Subtitle = $"Windows System Tool ({tool.Exe})",
                        ResultType = SearchResultType.Application,
                        IconGeometryKey = "GeoApps",
                        LaunchTarget = tool.Exe
                    });
                }

                // Index Start Menu shortcut links (.lnk)
                foreach (var dir in searchDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        foreach (var file in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
                        {
                            string appName = Path.GetFileNameWithoutExtension(file);
                            if (!_indexedApps.Any(a => a.Title.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                            {
                                _indexedApps.Add(new SearchResultItem
                                {
                                    Title = appName,
                                    Subtitle = "Installed Application",
                                    ResultType = SearchResultType.Application,
                                    IconGeometryKey = "GeoApps",
                                    LaunchTarget = file
                                });
                            }
                        }
                    }
                }

                _isAppsIndexed = true;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Index apps note: {ex.Message}");
            }
        }

        public async Task<List<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _predefinedItems.Take(8).ToList();
            }

            query = query.Trim();
            var results = new List<SearchResultItem>();

            // 1. Math calculation check (e.g. "25 * 4", "100 / 5 + 3")
            if (IsMathExpression(query, out double mathResult))
            {
                results.Add(new SearchResultItem
                {
                    Title = $"= {mathResult:N2}".Replace(".00", ""),
                    Subtitle = $"Calculated from: {query}",
                    ResultType = SearchResultType.Calculator,
                    IconGeometryKey = "GeoActivity",
                    LaunchTarget = mathResult.ToString(),
                    CustomAction = () => ClipboardService.Instance.CopyToClipboard(mathResult.ToString())
                });
            }

            // 2. Predefined Modules & Settings matches
            var lowerQuery = query.ToLowerInvariant();
            foreach (var item in _predefinedItems)
            {
                if (item.Title.ToLowerInvariant().Contains(lowerQuery) || item.Subtitle.ToLowerInvariant().Contains(lowerQuery))
                {
                    results.Add(item);
                }
            }

            // 3. Installed Apps matches
            foreach (var app in _indexedApps)
            {
                if (app.Title.ToLowerInvariant().Contains(lowerQuery))
                {
                    results.Add(app);
                    if (results.Count >= 20) break;
                }
            }

            // 4. Quick file search in User Common Folders (Desktop, Downloads, Documents)
            if (query.Length >= 3)
            {
                await Task.Run(() =>
                {
                    var userFolders = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                    };

                    int filesFound = 0;
                    foreach (var folder in userFolders)
                    {
                        if (Directory.Exists(folder))
                        {
                            try
                            {
                                var matches = Directory.EnumerateFiles(folder, $"*{query}*", SearchOption.TopDirectoryOnly);
                                foreach (var f in matches)
                                {
                                    results.Add(new SearchResultItem
                                    {
                                        Title = Path.GetFileName(f),
                                        Subtitle = Path.GetDirectoryName(f) ?? folder,
                                        ResultType = SearchResultType.FileOrFolder,
                                        IconGeometryKey = "GeoFileDrop",
                                        LaunchTarget = f
                                    });
                                    filesFound++;
                                    if (filesFound >= 5) break;
                                }
                            }
                            catch { }
                        }
                    }
                }, ct);
            }

            return results.Take(15).ToList();
        }

        private static bool IsMathExpression(string input, out double result)
        {
            result = 0;
            // Only test if contains math operators
            if (!input.Any(c => c == '+' || c == '-' || c == '*' || c == '/' || c == '%'))
            {
                return false;
            }

            try
            {
                var dt = new DataTable();
                var computeResult = dt.Compute(input, "");
                if (computeResult != null && double.TryParse(computeResult.ToString(), out result))
                {
                    return !double.IsInfinity(result) && !double.IsNaN(result);
                }
            }
            catch { }

            return false;
        }

        public void ExecuteResult(SearchResultItem item, Action<string>? onNavigateModule = null)
        {
            try
            {
                if (item.CustomAction != null)
                {
                    item.CustomAction.Invoke();
                    return;
                }

                if (item.ResultType == SearchResultType.NexIslandModule)
                {
                    onNavigateModule?.Invoke(item.LaunchTarget);
                    return;
                }

                // URL, setting URI or executable
                var psi = new ProcessStartInfo
                {
                    FileName = item.LaunchTarget,
                    Arguments = item.Arguments,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Failed to launch search result: {item.Title}", ex);
            }
        }
    }
}
