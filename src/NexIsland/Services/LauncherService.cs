using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NexIsland.Services
{
    public class LauncherItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string Category { get; set; } = "Apps";
        public string IconPath { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.Now;
    }

    public class LauncherService
    {
        private static LauncherService? _instance;
        public static LauncherService Instance => _instance ??= new LauncherService();

        private readonly List<LauncherItem> _items = new();
        private readonly string _storageFilePath;

        public IReadOnlyList<LauncherItem> Items => _items;

        public event Action? ItemsChanged;

        public LauncherService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _storageFilePath = Path.Combine(appData, "NexIsland", "launcher_items.json");
            LoadItems();

            if (_items.Count == 0)
            {
                SeedDefaults();
            }
        }

        private void SeedDefaults()
        {
            _items.Add(new LauncherItem { Title = "File Explorer", TargetPath = "explorer.exe", Category = "System", IsPinned = true });
            _items.Add(new LauncherItem { Title = "Task Manager", TargetPath = "taskmgr.exe", Category = "System", IsPinned = true });
            _items.Add(new LauncherItem { Title = "Calculator", TargetPath = "calc.exe", Category = "Utilities", IsPinned = true });
            _items.Add(new LauncherItem { Title = "Notepad", TargetPath = "notepad.exe", Category = "Utilities", IsPinned = true });
            _items.Add(new LauncherItem { Title = "Windows Settings", TargetPath = "ms-settings:", Category = "System", IsPinned = true });
            SaveItems();
        }

        public IEnumerable<LauncherItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _items.OrderByDescending(i => i.IsPinned).ThenByDescending(i => i.LastUsed);
            }

            return _items.Where(i => i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     i.TargetPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     i.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(i => i.IsPinned)
                         .ThenBy(i => i.Title);
        }

        public bool Launch(LauncherItem item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.TargetPath)) return false;

                // Safe protocol/URI check (e.g. https:// or ms-settings:)
                if (Uri.TryCreate(item.TargetPath, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "ms-settings"))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.TargetPath,
                        UseShellExecute = true
                    });
                    item.LastUsed = DateTime.Now;
                    SaveItems();
                    return true;
                }

                // File or system command check
                var psi = new ProcessStartInfo
                {
                    FileName = item.TargetPath,
                    Arguments = item.Arguments,
                    UseShellExecute = true
                };

                Process.Start(psi);
                item.LastUsed = DateTime.Now;
                SaveItems();
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Failed to launch {item.Title} ({item.TargetPath})", ex);
                return false;
            }
        }

        public void AddItem(string title, string targetPath, string category = "Custom", bool isPinned = false)
        {
            var existing = _items.FirstOrDefault(i => i.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                      (!string.IsNullOrEmpty(targetPath) && i.TargetPath.Equals(targetPath.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                existing.LastUsed = DateTime.Now;
                if (isPinned) existing.IsPinned = true;
                SaveItems();
                ItemsChanged?.Invoke();
                return;
            }

            _items.Add(new LauncherItem
            {
                Title = title.Trim(),
                TargetPath = targetPath.Trim(),
                Category = category,
                IsPinned = isPinned,
                LastUsed = DateTime.Now
            });
            SaveItems();
            ItemsChanged?.Invoke();
        }

        public void RemoveItem(string id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                SaveItems();
                ItemsChanged?.Invoke();
            }
        }

        public void TogglePin(string id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                SaveItems();
                ItemsChanged?.Invoke();
            }
        }

        private void LoadItems()
        {
            try
            {
                if (File.Exists(_storageFilePath))
                {
                    var json = File.ReadAllText(_storageFilePath);
                    var list = JsonSerializer.Deserialize<List<LauncherItem>>(json);
                    if (list != null)
                    {
                        _items.Clear();
                        var unique = list
                            .GroupBy(i => i.Title.Trim().ToLowerInvariant())
                            .Select(g => g.First())
                            .ToList();
                        _items.AddRange(unique);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to load launcher items", ex);
            }
        }

        private void SaveItems()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storageFilePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to save launcher items", ex);
            }
        }
    }
}
