using System;
using System.IO;
using System.Text.Json;

namespace NexIsland.Settings
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private readonly string _settingsFilePath;
        private AppSettings _settings;

        public event Action<AppSettings>? SettingsChanged;

        public AppSettings Current => _settings;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "NexIsland");
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch { }

            _settingsFilePath = Path.Combine(dir, "settings.json");
            _settings = Load();
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        var defaultOrder = new AppSettings().ModuleOrder;
                        if (loaded.ModuleOrder == null || loaded.ModuleOrder.Count < defaultOrder.Count)
                        {
                            loaded.ModuleOrder = defaultOrder;
                        }
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Services.LoggingService.Instance.Error("Failed to load settings, using defaults.", ex);
            }

            return new AppSettings();
        }

        public void Save(AppSettings? settings = null)
        {
            try
            {
                if (settings != null)
                {
                    _settings = settings;
                }

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                SettingsChanged?.Invoke(_settings);
            }
            catch (Exception ex)
            {
                Services.LoggingService.Instance.Error("Failed to save settings.", ex);
            }
        }

        public void Reset()
        {
            _settings = new AppSettings();
            Save();
        }
    }
}
