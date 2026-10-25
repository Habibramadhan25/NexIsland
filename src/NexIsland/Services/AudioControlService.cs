using System;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace NexIsland.Services
{
    public class AudioControlService : IDisposable, IMMNotificationClient
    {
        private static AudioControlService? _instance;
        public static AudioControlService Instance => _instance ??= new AudioControlService();

        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _defaultPlaybackDevice;
        private AudioEndpointVolume? _endpointVolume;
        private bool _isDisposed;
        private bool _isInternalChange;

        public int MasterVolume { get; private set; } = 50;
        public bool IsMuted { get; private set; }
        public string DeviceName { get; private set; } = "Windows Audio Output";
        public bool IsAvailable => _endpointVolume != null;

        public event Action<int, bool>? VolumeChanged;
        public event Action<string>? DeviceChanged;

        public AudioControlService()
        {
            InitializeDevice();
        }

        private void InitializeDevice()
        {
            try
            {
                UnsubscribeVolumeEvents();

                _deviceEnumerator ??= new MMDeviceEnumerator();
                try
                {
                    _deviceEnumerator.RegisterEndpointNotificationCallback(this);
                }
                catch { }

                _defaultPlaybackDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                if (_defaultPlaybackDevice != null)
                {
                    DeviceName = _defaultPlaybackDevice.FriendlyName ?? "Speakers";
                    _endpointVolume = _defaultPlaybackDevice.AudioEndpointVolume;

                    if (_endpointVolume != null)
                    {
                        MasterVolume = (int)Math.Round(_endpointVolume.MasterVolumeLevelScalar * 100.0f);
                        IsMuted = _endpointVolume.Mute;
                        _endpointVolume.OnVolumeNotification += OnEndpointVolumeNotification;
                    }

                    DeviceChanged?.Invoke(DeviceName);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"Could not initialize Windows audio endpoint: {ex.Message}");
                _endpointVolume = null;
            }
        }

        private void OnEndpointVolumeNotification(AudioVolumeNotificationData data)
        {
            if (_isInternalChange) return;

            int newVol = Math.Clamp((int)Math.Round(data.MasterVolume * 100.0f), 0, 100);
            bool newMute = data.Muted;

            MasterVolume = newVol;
            IsMuted = newMute;

            VolumeChanged?.Invoke(newVol, newMute);
        }

        public void SetVolume(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            MasterVolume = percent;

            if (_endpointVolume == null)
            {
                InitializeDevice();
            }

            if (_endpointVolume != null)
            {
                try
                {
                    _isInternalChange = true;
                    _endpointVolume.MasterVolumeLevelScalar = percent / 100.0f;

                    // If unmuting when adjusting volume upwards from 0
                    if (percent > 0 && _endpointVolume.Mute)
                    {
                        _endpointVolume.Mute = false;
                        IsMuted = false;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Debug($"Set volume exception: {ex.Message}");
                }
                finally
                {
                    _isInternalChange = false;
                }
            }

            VolumeChanged?.Invoke(percent, IsMuted);
        }

        public void ToggleMute()
        {
            if (_endpointVolume == null)
            {
                InitializeDevice();
            }

            if (_endpointVolume != null)
            {
                try
                {
                    _isInternalChange = true;
                    bool newMute = !_endpointVolume.Mute;
                    _endpointVolume.Mute = newMute;
                    IsMuted = newMute;
                    VolumeChanged?.Invoke(MasterVolume, newMute);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Debug($"Toggle mute exception: {ex.Message}");
                }
                finally
                {
                    _isInternalChange = false;
                }
            }
        }

        private void UnsubscribeVolumeEvents()
        {
            if (_endpointVolume != null)
            {
                try
                {
                    _endpointVolume.OnVolumeNotification -= OnEndpointVolumeNotification;
                }
                catch { }
                _endpointVolume = null;
            }

            if (_defaultPlaybackDevice != null)
            {
                try { _defaultPlaybackDevice.Dispose(); } catch { }
                _defaultPlaybackDevice = null;
            }
        }

        #region IMMNotificationClient Implementation
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && (role == Role.Multimedia || role == Role.Console))
            {
                // Audio device switched (e.g. Speakers to Headphones)
                InitializeDevice();
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            UnsubscribeVolumeEvents();
            if (_deviceEnumerator != null)
            {
                try
                {
                    _deviceEnumerator.UnregisterEndpointNotificationCallback(this);
                    _deviceEnumerator.Dispose();
                }
                catch { }
                _deviceEnumerator = null;
            }
        }
    }
}
