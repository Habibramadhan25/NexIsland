using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NexIsland.Plugins
{
    public class PluginMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconEmoji { get; set; } = "🧩";
    }

    public interface IPluginContext
    {
        void PostNotification(string title, string message, string emoji = "🧩");
        void LogInfo(string message);
        void LogError(string message, Exception? ex = null);
    }

    public interface INexIslandPlugin
    {
        PluginMetadata Metadata { get; }
        bool IsEnabled { get; set; }
        Task InitializeAsync(IPluginContext context);
        Task StartAsync();
        Task StopAsync();
    }

    public class PluginContext : IPluginContext
    {
        private readonly string _pluginId;

        public PluginContext(string pluginId)
        {
            _pluginId = pluginId;
        }

        public void PostNotification(string title, string message, string emoji = "🧩")
        {
            Services.NotificationService.Instance.PostNotification(title, message, $"Plugin: {_pluginId}", emoji);
        }

        public void LogInfo(string message)
        {
            Services.LoggingService.Instance.Info($"[{_pluginId}] {message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            Services.LoggingService.Instance.Error($"[{_pluginId}] {message}", ex);
        }
    }

    public class PluginService
    {
        private static PluginService? _instance;
        public static PluginService Instance => _instance ??= new PluginService();

        private readonly List<INexIslandPlugin> _plugins = new();
        public IReadOnlyList<INexIslandPlugin> Plugins => _plugins;

        public event Action? PluginsUpdated;

        public void RegisterPlugin(INexIslandPlugin plugin)
        {
            try
            {
                _plugins.Add(plugin);
                PluginsUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Services.LoggingService.Instance.Error($"Failed to register plugin {plugin.Metadata.Name}", ex);
            }
        }

        public async Task InitializeAllAsync()
        {
            // Register built-in sample plugin: DrinkWaterReminderPlugin & SystemUptimePlugin
            RegisterPlugin(new DrinkWaterReminderPlugin());

            foreach (var plugin in _plugins)
            {
                try
                {
                    var ctx = new PluginContext(plugin.Metadata.Id);
                    await plugin.InitializeAsync(ctx);
                    if (plugin.IsEnabled)
                    {
                        await plugin.StartAsync();
                    }
                }
                catch (Exception ex)
                {
                    Services.LoggingService.Instance.Error($"Plugin {plugin.Metadata.Name} failed to start", ex);
                }
            }
        }

        public async Task TogglePluginAsync(string pluginId, bool enable)
        {
            var plugin = _plugins.Find(p => p.Metadata.Id == pluginId);
            if (plugin != null)
            {
                try
                {
                    plugin.IsEnabled = enable;
                    if (enable) await plugin.StartAsync();
                    else await plugin.StopAsync();
                    PluginsUpdated?.Invoke();
                }
                catch (Exception ex)
                {
                    Services.LoggingService.Instance.Error($"Error toggling plugin {pluginId}", ex);
                }
            }
        }
    }

    public class DrinkWaterReminderPlugin : INexIslandPlugin
    {
        private IPluginContext? _context;
        private System.Windows.Threading.DispatcherTimer? _timer;

        public PluginMetadata Metadata { get; } = new()
        {
            Id = "nex.hydrate",
            Name = "Hydration Reminder",
            Version = "1.0.0",
            Author = "NexIsland Team",
            Description = "Periodically reminds you to drink water and take care of your health.",
            IconEmoji = "💧"
        };

        public bool IsEnabled { get; set; } = true;

        public Task InitializeAsync(IPluginContext context)
        {
            _context = context;
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromHours(1) // Every hour
            };
            _timer.Tick += (s, e) =>
            {
                _context?.PostNotification("Hydration Break", "Stay healthy! Remember to drink a glass of fresh water.", "💧");
            };
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            _timer?.Start();
            _context?.LogInfo("Hydration plugin started");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _timer?.Stop();
            _context?.LogInfo("Hydration plugin stopped");
            return Task.CompletedTask;
        }
    }
}
