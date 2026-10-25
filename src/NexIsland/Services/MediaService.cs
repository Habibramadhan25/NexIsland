using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NexIsland.Services
{
    public class MediaTrackInfo
    {
        public bool HasActiveSession { get; set; }
        public string Title { get; set; } = "No media playing";
        public string Artist { get; set; } = "Ready for music";
        public string Album { get; set; } = string.Empty;
        public bool IsPlaying { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public bool HasValidDuration => Duration > TimeSpan.Zero;
        public BitmapImage? Thumbnail { get; set; }
        public string SourceAppId { get; set; } = string.Empty;

        public double ProgressFraction => HasValidDuration
            ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0)
            : 0.0;
    }

    public class MediaService : IDisposable
    {
        private static MediaService? _instance;
        public static MediaService Instance => _instance ??= new MediaService();

        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly DispatcherTimer _playbackTimer;

        private TimeSpan _rawPosition = TimeSpan.Zero;
        private TimeSpan _startTime = TimeSpan.Zero;
        private TimeSpan _endTime = TimeSpan.Zero;
        private DateTimeOffset _lastTimelineUpdate = DateTimeOffset.MinValue;
        private string _lastTrackSignature = string.Empty;

        public MediaTrackInfo CurrentTrack { get; private set; } = new();

        public event Action<MediaTrackInfo>? TrackUpdated;
        public event Action<bool>? PlaybackStateChanged;
        public event Action<TimeSpan, TimeSpan, double>? PositionTick;

        public MediaService()
        {
            _playbackTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(250) // Smooth 4Hz ticker for position & lyrics
            };
            _playbackTimer.Tick += OnPlaybackTimerTick;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                    AttachToCurrentSession();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("GSMTC MediaService failed to initialize", ex);
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            AttachToCurrentSession();
        }

        private void AttachToCurrentSession()
        {
            try
            {
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                }

                _currentSession = _sessionManager?.GetCurrentSession();

                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                    _ = RefreshMediaInfoAsync();
                }
                else
                {
                    _playbackTimer.Stop();
                    _rawPosition = TimeSpan.Zero;
                    _startTime = TimeSpan.Zero;
                    _endTime = TimeSpan.Zero;
                    _lastTimelineUpdate = DateTimeOffset.MinValue;
                    CurrentTrack = new MediaTrackInfo { HasActiveSession = false };
                    TrackUpdated?.Invoke(CurrentTrack);
                    LyricsService.Instance.Clear();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Error attaching to GSMTC session", ex);
            }
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = RefreshMediaInfoAsync();
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _ = RefreshMediaInfoAsync();
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            UpdateTimelineSnapshot();
        }

        private void UpdateTimelineSnapshot()
        {
            if (_currentSession == null) return;

            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                if (timeline != null)
                {
                    ApplyTimeline(timeline, isNewTrack: false);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Timeline update exception: {ex.Message}");
            }
        }

        private void ApplyTimeline(GlobalSystemMediaTransportControlsSessionTimelineProperties timeline, bool isNewTrack)
        {
            _rawPosition = timeline.Position;

            // Only update StartTime & EndTime if timeline reports a positive EndTime,
            // or if we explicitly changed to a new track. This guarantees that temporary
            // intermediate events returning EndTime = 0 do not zero-out valid cached track durations.
            if (timeline.EndTime > TimeSpan.Zero)
            {
                _startTime = timeline.StartTime;
                _endTime = timeline.EndTime;
            }
            else if (isNewTrack)
            {
                _startTime = timeline.StartTime;
                _endTime = TimeSpan.Zero;
            }

            _lastTimelineUpdate = timeline.LastUpdatedTime != DateTimeOffset.MinValue
                ? timeline.LastUpdatedTime
                : DateTimeOffset.UtcNow;

            CalculateCurrentPositionAndDuration();
        }

        private void OnPlaybackTimerTick(object? sender, EventArgs e)
        {
            if (!CurrentTrack.HasActiveSession || !CurrentTrack.IsPlaying) return;

            CalculateCurrentPositionAndDuration();
        }

        private void CalculateCurrentPositionAndDuration()
        {
            TimeSpan duration = TimeSpan.Zero;
            if (_endTime > _startTime)
            {
                duration = _endTime - _startTime;
            }
            else if (_endTime > TimeSpan.Zero)
            {
                duration = _endTime;
            }

            // Calculate base position relative to StartTime (for streams with non-zero start offset)
            TimeSpan basePos = _rawPosition;
            if (_startTime > TimeSpan.Zero && _rawPosition >= _startTime)
            {
                basePos = _rawPosition - _startTime;
            }

            TimeSpan currentPos = basePos;
            if (CurrentTrack.IsPlaying && _lastTimelineUpdate != DateTimeOffset.MinValue)
            {
                var elapsed = DateTimeOffset.UtcNow - _lastTimelineUpdate;
                if (elapsed > TimeSpan.Zero)
                {
                    currentPos = basePos + elapsed;
                }
            }

            if (duration > TimeSpan.Zero && currentPos > duration)
            {
                currentPos = duration;
            }
            if (currentPos < TimeSpan.Zero)
            {
                currentPos = TimeSpan.Zero;
            }

            CurrentTrack.Position = currentPos;
            CurrentTrack.Duration = duration;

            PositionTick?.Invoke(currentPos, duration, CurrentTrack.ProgressFraction);
            LyricsService.Instance.UpdatePosition(currentPos);
        }

        public async Task RefreshMediaInfoAsync()
        {
            if (_currentSession == null)
            {
                _playbackTimer.Stop();
                CurrentTrack = new MediaTrackInfo { HasActiveSession = false };
                TrackUpdated?.Invoke(CurrentTrack);
                LyricsService.Instance.Clear();
                return;
            }

            try
            {
                var props = await _currentSession.TryGetMediaPropertiesAsync();
                var playback = _currentSession.GetPlaybackInfo();
                var timeline = _currentSession.GetTimelineProperties();

                bool isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var sourceId = _currentSession.SourceAppUserModelId ?? "Media Player";

                BitmapImage? thumb = null;
                if (props?.Thumbnail != null)
                {
                    thumb = await LoadThumbnailAsync(props.Thumbnail);
                }

                var title = !string.IsNullOrWhiteSpace(props?.Title) ? props.Title : "Unknown Track";
                var artist = !string.IsNullOrWhiteSpace(props?.Artist) ? props.Artist : "Unknown Artist";

                // Track signature changed -> new track
                var signature = $"{title}|{artist}";
                bool isNewTrack = signature != _lastTrackSignature;
                if (isNewTrack)
                {
                    _lastTrackSignature = signature;
                    LyricsService.Instance.SetSampleLyricsForTrack(title, artist);
                }

                if (timeline != null)
                {
                    ApplyTimeline(timeline, isNewTrack);
                }
                else if (isNewTrack)
                {
                    _startTime = TimeSpan.Zero;
                    _endTime = TimeSpan.Zero;
                    _rawPosition = TimeSpan.Zero;
                    _lastTimelineUpdate = DateTimeOffset.UtcNow;
                }

                TimeSpan duration = TimeSpan.Zero;
                if (_endTime > _startTime)
                {
                    duration = _endTime - _startTime;
                }
                else if (_endTime > TimeSpan.Zero)
                {
                    duration = _endTime;
                }

                CurrentTrack = new MediaTrackInfo
                {
                    HasActiveSession = true,
                    Title = title,
                    Artist = artist,
                    Album = props?.AlbumTitle ?? string.Empty,
                    IsPlaying = isPlaying,
                    Position = CurrentTrack.Position,
                    Duration = duration,
                    Thumbnail = thumb,
                    SourceAppId = sourceId
                };

                if (isPlaying)
                {
                    if (!_playbackTimer.IsEnabled) _playbackTimer.Start();
                }
                else
                {
                    _playbackTimer.Stop();
                }

                CalculateCurrentPositionAndDuration();

                TrackUpdated?.Invoke(CurrentTrack);
                PlaybackStateChanged?.Invoke(isPlaying);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Media info refresh note: {ex.Message}");
            }
        }

        private static async Task<BitmapImage?> LoadThumbnailAsync(IRandomAccessStreamReference streamRef)
        {
            try
            {
                using var stream = await streamRef.OpenReadAsync();
                if (stream != null && stream.Size > 0)
                {
                    var bytes = new byte[stream.Size];
                    using var reader = new DataReader(stream);
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(bytes);

                    var image = new BitmapImage();
                    using var ms = new MemoryStream(bytes);
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch { }
            return null;
        }

        public async Task TogglePlayPauseAsync()
        {
            if (_currentSession == null) return;
            try
            {
                await _currentSession.TryTogglePlayPauseAsync();
                await Task.Delay(100);
                await RefreshMediaInfoAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to toggle play/pause", ex);
            }
        }

        public async Task NextTrackAsync()
        {
            if (_currentSession == null) return;
            try
            {
                await _currentSession.TrySkipNextAsync();
                await Task.Delay(250);
                await RefreshMediaInfoAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to skip next track", ex);
            }
        }

        public async Task PreviousTrackAsync()
        {
            if (_currentSession == null) return;
            try
            {
                await _currentSession.TrySkipPreviousAsync();
                await Task.Delay(250);
                await RefreshMediaInfoAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to skip previous track", ex);
            }
        }

        public async Task SeekToFractionAsync(double fraction)
        {
            if (_currentSession == null || CurrentTrack.Duration <= TimeSpan.Zero) return;
            try
            {
                fraction = Math.Clamp(fraction, 0.0, 1.0);
                var targetTicks = (long)(CurrentTrack.Duration.Ticks * fraction);
                var targetPos = TimeSpan.FromTicks(targetTicks);

                var absoluteTicks = _startTime.Ticks + targetTicks;
                await _currentSession.TryChangePlaybackPositionAsync(absoluteTicks);
                _rawPosition = TimeSpan.FromTicks(absoluteTicks);
                _lastTimelineUpdate = DateTimeOffset.UtcNow;
                CalculateCurrentPositionAndDuration();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to seek media by fraction", ex);
            }
        }

        public async Task SeekToPositionAsync(TimeSpan targetPos)
        {
            if (_currentSession == null) return;
            try
            {
                var absoluteTicks = _startTime.Ticks + targetPos.Ticks;
                await _currentSession.TryChangePlaybackPositionAsync(absoluteTicks);
                _rawPosition = TimeSpan.FromTicks(absoluteTicks);
                _lastTimelineUpdate = DateTimeOffset.UtcNow;
                CalculateCurrentPositionAndDuration();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to seek media by position", ex);
            }
        }

        public void Dispose()
        {
            _playbackTimer.Stop();
        }
    }
}
