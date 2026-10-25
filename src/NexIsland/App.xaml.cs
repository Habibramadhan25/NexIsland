using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using NexIsland.Services;
using NexIsland.ViewModels;

namespace NexIsland
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = "NexIsland_SingleInstance_App_Mutex_2026";
        private const string EventName = "NexIsland_Activate_Event_2026";
        private Mutex? _mutex;
        private EventWaitHandle? _activateEvent;
        private bool _isDisposed;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                // Another instance is already running -> signal it to expand and bring to foreground
                try
                {
                    if (EventWaitHandle.TryOpenExisting(EventName, out var existingEvent))
                    {
                        existingEvent.Set();
                    }
                }
                catch { }

                Shutdown(0);
                return;
            }

            // Create activation event listener for subsequent launches
            try
            {
                _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    while (!_isDisposed)
                    {
                        try
                        {
                            if (_activateEvent.WaitOne())
                            {
                                if (_isDisposed) break;
                                Dispatcher.Invoke(() =>
                                {
                                    if (MainWindow is MainWindow mw)
                                    {
                                        mw.ViewModel.State = IslandVisualState.Expanded;
                                        mw.UpdatePosition();
                                        mw.Show();
                                        mw.Activate();
                                        var hwnd = new WindowInteropHelper(mw).Handle;
                                        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, (int)mw.Left, (int)mw.Top, (int)mw.Width, (int)mw.Height, NativeMethods.SWP_SHOWWINDOW);
                                    }
                                });
                            }
                        }
                        catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"Could not create single-instance event: {ex.Message}");
            }

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LoggingService.Instance.Error("Fatal AppDomain Unhandled Exception", ex);
                }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LoggingService.Instance.Error("Dispatcher Unhandled Exception", args.Exception);
                args.Handled = true; // Prevent crash, recover gracefully
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _isDisposed = true;
            _activateEvent?.Dispose();
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
