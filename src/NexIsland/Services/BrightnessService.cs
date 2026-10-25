using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public class BrightnessService : IDisposable
    {
        private static BrightnessService? _instance;
        public static BrightnessService Instance => _instance ??= new BrightnessService();

        private readonly DispatcherTimer _syncTimer;
        private readonly object _lock = new();
        private CancellationTokenSource? _debounceCts;
        private bool _isDisposed;
        private bool _isApplying;

        public bool IsSupported { get; private set; } = true;
        public int CurrentBrightness { get; private set; } = 50;

        public event Action<int>? BrightnessChanged;
        public event Action<bool>? AvailabilityChanged;

        public BrightnessService()
        {
            _syncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _syncTimer.Tick += async (s, e) => await RefreshBrightnessAsync();

            // Initial query in background
            Task.Run(async () => await RefreshBrightnessAsync());
            _syncTimer.Start();
        }

        public async Task RefreshBrightnessAsync()
        {
            if (_isApplying) return;

            try
            {
                int? detected = await Task.Run(() => ReadWmiBrightness());

                if (detected.HasValue)
                {
                    bool wasSupported = IsSupported;
                    IsSupported = true;
                    CurrentBrightness = Math.Clamp(detected.Value, 0, 100);

                    if (!wasSupported)
                    {
                        AvailabilityChanged?.Invoke(true);
                    }

                    BrightnessChanged?.Invoke(CurrentBrightness);
                }
                else
                {
                    if (IsSupported)
                    {
                        IsSupported = false;
                        AvailabilityChanged?.Invoke(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Brightness query note: {ex.Message}");
                if (IsSupported)
                {
                    IsSupported = false;
                    AvailabilityChanged?.Invoke(false);
                }
            }
        }

        private static int? ReadWmiBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
                using var collection = searcher.Get();

                foreach (ManagementObject mo in collection)
                {
                    if (mo["CurrentBrightness"] != null)
                    {
                        return Convert.ToInt32(mo["CurrentBrightness"]);
                    }
                }
            }
            catch
            {
                // WMI brightness is not supported on this monitor or display driver
            }
            return null;
        }

        public void SetBrightness(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            CurrentBrightness = percent;
            BrightnessChanged?.Invoke(percent);

            if (!IsSupported) return;

            // Debounce rapid slider inputs (80ms) to avoid overwhelming the display driver
            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
            }

            var ct = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(80, ct);
                    if (ct.IsCancellationRequested) return;

                    _isApplying = true;
                    ApplyWmiBrightness(percent);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    LoggingService.Instance.Warn($"Failed to set WMI brightness: {ex.Message}");
                }
                finally
                {
                    _isApplying = false;
                }
            }, ct);
        }

        private static void ApplyWmiBrightness(int percent)
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var collection = searcher.Get();

            bool appliedAny = false;
            foreach (ManagementObject mo in collection)
            {
                // Timeout = 1 second, Brightness = 0-100%
                mo.InvokeMethod("WmiSetBrightness", new object[] { 1u, (byte)percent });
                appliedAny = true;
            }

            if (!appliedAny)
            {
                throw new NotSupportedException("No WmiMonitorBrightnessMethods instances found.");
            }
        }

        public void OpenDisplaySettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:display",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Could not launch Display Settings", ex);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _syncTimer.Stop();
            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
            }
        }
    }
}
