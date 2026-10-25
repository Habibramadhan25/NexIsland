using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace NexIsland.Services
{
    public class ScreenInfo
    {
        public int Index { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public Rect Bounds { get; set; }
        public Rect WorkingArea { get; set; }
        public bool IsPrimary { get; set; }
    }

    public static class ScreenHelper
    {
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        public static List<ScreenInfo> GetScreens()
        {
            var screens = new List<ScreenInfo>();
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
            {
                var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    bool isPrimary = (mi.dwFlags & 1) != 0;
                    screens.Add(new ScreenInfo
                    {
                        Index = index++,
                        DeviceName = mi.szDevice,
                        Bounds = new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height),
                        WorkingArea = new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height),
                        IsPrimary = isPrimary
                    });
                }
                return true;
            }, IntPtr.Zero);

            if (screens.Count == 0)
            {
                // Fallback to primary virtual screen
                screens.Add(new ScreenInfo
                {
                    Index = 0,
                    DeviceName = "Primary",
                    Bounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                    WorkingArea = new Rect(0, 0, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height),
                    IsPrimary = true
                });
            }

            return screens;
        }

        public static (double Left, double Top) CalculateTopCenterPosition(int monitorIndex, double windowWidth, double yOffset, Window window)
        {
            try
            {
                if (double.IsNaN(windowWidth) || windowWidth <= 0)
                {
                    windowWidth = 240; // Default compact width
                }

                // If monitor index is 0 or primary, use WPF native PrimaryScreenWidth directly for 100% accuracy in WPF DIPs
                if (monitorIndex <= 0)
                {
                    double primaryWidth = SystemParameters.PrimaryScreenWidth;
                    double left = (primaryWidth - windowWidth) / 2.0;
                    double top = yOffset;
                    LoggingService.Instance.Info($"Position Primary: Left={left}, Top={top}, PrimaryWidth={primaryWidth}, WindowWidth={windowWidth}");
                    return (left, top);
                }

                var screens = GetScreens();
                if (monitorIndex >= screens.Count) monitorIndex = 0;
                var screen = screens[monitorIndex];

                var dpi = VisualTreeHelper.GetDpi(window);
                double dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                double screenLeftDip = screen.Bounds.Left / dpiScaleX;
                double screenWidthDip = screen.Bounds.Width / dpiScaleX;
                double screenTopDip = screen.Bounds.Top / dpiScaleY;

                double l = screenLeftDip + (screenWidthDip - windowWidth) / 2.0;
                double t = screenTopDip + yOffset;
                LoggingService.Instance.Info($"Position Monitor {monitorIndex}: Left={l}, Top={t}");
                return (l, t);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"Position calculation fallback: {ex.Message}");
                double fallbackWidth = SystemParameters.PrimaryScreenWidth > 0 ? SystemParameters.PrimaryScreenWidth : 1366;
                return ((fallbackWidth - 240) / 2.0, yOffset);
            }
        }
    }
}
