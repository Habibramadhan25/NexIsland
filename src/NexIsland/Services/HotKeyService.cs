using System;
using System.Windows;
using System.Windows.Interop;

namespace NexIsland.Services
{
    public class HotKeyService : IDisposable
    {
        private static HotKeyService? _instance;
        public static HotKeyService Instance => _instance ??= new HotKeyService();

        private const int SEARCH_HOTKEY_ID = 9001;
        private const uint VK_SPACE = 0x20;

        private IntPtr _hWnd;
        private HwndSource? _source;
        private bool _isRegistered = false;

        public event Action? SearchHotKeyPressed;

        public void Initialize(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                _hWnd = helper.Handle;

                _source = HwndSource.FromHwnd(_hWnd);
                _source?.AddHook(HwndHook);

                // Register Alt + Space (MOD_ALT | MOD_NOREPEAT, VK_SPACE)
                _isRegistered = NativeMethods.RegisterHotKey(
                    _hWnd,
                    SEARCH_HOTKEY_ID,
                    NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                    VK_SPACE
                );

                if (_isRegistered)
                {
                    LoggingService.Instance.Info("Global Hotkey [Alt + Space] registered successfully for Universal Search.");
                }
                else
                {
                    LoggingService.Instance.Warn("Could not register [Alt + Space]. Another application might be using it; fallback activation is available in Island.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to register global hotkey", ex);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == SEARCH_HOTKEY_ID)
            {
                SearchHotKeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isRegistered && _hWnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hWnd, SEARCH_HOTKEY_ID);
                _isRegistered = false;
            }
            _source?.RemoveHook(HwndHook);
        }
    }
}
