using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace NexIsland.Services
{
    public static class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NexIsland";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to check startup registry key", ex);
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                        LoggingService.Instance.Info($"Startup enabled for {exePath}");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                    LoggingService.Instance.Info("Startup disabled");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to update startup registry key", ex);
            }
        }
    }
}
