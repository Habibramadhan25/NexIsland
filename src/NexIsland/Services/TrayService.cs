using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NexIsland.ViewModels;

namespace NexIsland.Services
{
    public class TrayService : IDisposable
    {
        private static TrayService? _instance;
        public static TrayService Instance => _instance ??= new TrayService();

        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        public void Initialize(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;

            try
            {
                _notifyIcon = new NotifyIcon();

                // Load official capsule icon
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string iconPath = Path.Combine(baseDir, "Assets", "Branding", "NexIslandIcon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }

                _notifyIcon.Text = "NexIsland Dynamic Island";
                _notifyIcon.Visible = true;

                // Left click toggles island
                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _mainWindow.Dispatcher.Invoke(() =>
                        {
                            if (_mainWindow.ViewModel.IsCompact)
                            {
                                _mainWindow.ViewModel.State = IslandVisualState.Expanded;
                            }
                            else
                            {
                                _mainWindow.ViewModel.State = IslandVisualState.Compact;
                            }
                            _mainWindow.UpdatePosition();
                            _mainWindow.Activate();
                        });
                    }
                };

                // Context menu on right click
                var contextMenu = new ContextMenuStrip();
                var toggleItem = new ToolStripMenuItem("🏝️ Buka / Kecilkan Island", null, (s, e) =>
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.ViewModel.State = (_mainWindow.ViewModel.IsCompact)
                            ? IslandVisualState.Expanded
                            : IslandVisualState.Compact;
                        _mainWindow.UpdatePosition();
                        _mainWindow.Activate();
                    });
                });

                var settingsItem = new ToolStripMenuItem("⚙️ Pengaturan NexIsland (Tema & Preferensi)", null, (s, e) =>
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.ViewModel.CurrentModule = IslandModule.Themes;
                        _mainWindow.ViewModel.State = IslandVisualState.Expanded;
                        _mainWindow.UpdatePosition();
                        _mainWindow.Activate();
                    });
                });

                var exitItem = new ToolStripMenuItem("❌ Keluar NexIsland", null, (s, e) =>
                {
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        Dispose();
                        System.Windows.Application.Current.Shutdown();
                    });
                });

                contextMenu.Items.Add(toggleItem);
                contextMenu.Items.Add(settingsItem);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;
                LoggingService.Instance.Info("System Tray icon initialized successfully");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to initialize System Tray icon", ex);
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
