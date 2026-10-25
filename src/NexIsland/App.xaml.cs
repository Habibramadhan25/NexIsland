using System;
using System.Windows;
using NexIsland.Services;

namespace NexIsland
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
    }
}
