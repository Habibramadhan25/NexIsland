using System;
using System.Windows.Threading;
using NAudio.Wave;

namespace NexIsland.Services
{
    public class AudioVisualizerService : IDisposable
    {
        private static AudioVisualizerService? _instance;
        public static AudioVisualizerService Instance => _instance ??= new AudioVisualizerService();

        private WasapiLoopbackCapture? _capture;
        private readonly DispatcherTimer _decayTimer;
        private readonly float[] _bars;
        private readonly float[] _targetBars;
        private readonly object _lock = new();

        private bool _isCapturing;
        private bool _hasAudio;
        private int _silentCount;

        public int BarsCount => _bars.Length;
        public float[] CurrentBars => _bars;
        public bool IsActive => _isCapturing;

        public event Action<float[]>? BarsUpdated;

        public AudioVisualizerService(int barCount = 12)
        {
            _bars = new float[barCount];
            _targetBars = new float[barCount];

            _decayTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS for silky visualizer
            };
            _decayTimer.Tick += OnDecayTick;
        }

        public void Start()
        {
            if (_isCapturing) return;

            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnAudioDataAvailable;
                _capture.RecordingStopped += (s, a) => _isCapturing = false;
                _capture.StartRecording();
                _isCapturing = true;
                _decayTimer.Start();
                LoggingService.Instance.Info("AudioVisualizer WASAPI loopback capture started");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"Could not start WASAPI audio capture (Audio output may be disabled): {ex.Message}");
                _isCapturing = false;
            }
        }

        public void Stop()
        {
            if (!_isCapturing) return;

            try
            {
                _isCapturing = false;
                _decayTimer.Stop();
                if (_capture != null)
                {
                    _capture.StopRecording();
                    _capture.Dispose();
                    _capture = null;
                }

                lock (_lock)
                {
                    Array.Clear(_bars, 0, _bars.Length);
                    Array.Clear(_targetBars, 0, _targetBars.Length);
                }
                BarsUpdated?.Invoke(_bars);
                LoggingService.Instance.Info("AudioVisualizer stopped");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Error stopping audio visualizer", ex);
            }
        }

        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || _capture == null) return;

            try
            {
                var buffer = new WaveBuffer(e.Buffer);
                int sampleCount = e.BytesRecorded / 4; // 32-bit float
                if (sampleCount <= 0) return;

                int bins = _bars.Length;
                int chunkSize = Math.Max(1, sampleCount / bins);
                float maxVal = 0;

                lock (_lock)
                {
                    for (int i = 0; i < bins; i++)
                    {
                        float sum = 0;
                        int start = i * chunkSize;
                        int end = Math.Min(sampleCount, start + chunkSize);
                        int count = 0;

                        for (int j = start; j < end; j++)
                        {
                            float sample = Math.Abs(buffer.FloatBuffer[j]);
                            sum += sample;
                            count++;
                        }

                        float avg = count > 0 ? (sum / count) * 4.0f : 0f;
                        avg = Math.Clamp(avg, 0f, 1f);
                        if (avg > maxVal) maxVal = avg;

                        // Give more boost to bass & mid frequencies for aesthetic symmetry
                        float freqWeight = 1.0f + (float)Math.Sin((double)i / bins * Math.PI) * 0.4f;
                        _targetBars[i] = Math.Clamp(avg * freqWeight, 0f, 1f);
                    }
                }

                _hasAudio = maxVal > 0.01f;
            }
            catch
            {
                // Ignore buffer conversion anomalies
            }
        }

        private void OnDecayTick(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                bool anyNonZero = false;
                for (int i = 0; i < _bars.Length; i++)
                {
                    if (_targetBars[i] > _bars[i])
                    {
                        // Fast attack
                        _bars[i] += (_targetBars[i] - _bars[i]) * 0.6f;
                    }
                    else
                    {
                        // Smooth decay
                        _bars[i] -= (_bars[i] - _targetBars[i]) * 0.25f;
                    }

                    if (_bars[i] < 0.02f) _bars[i] = 0f;
                    else anyNonZero = true;
                }

                if (!anyNonZero)
                {
                    _silentCount++;
                    if (_silentCount > 60 && !_hasAudio)
                    {
                        // Audio idle, relax decay tick
                    }
                }
                else
                {
                    _silentCount = 0;
                }
            }

            BarsUpdated?.Invoke((float[])_bars.Clone());
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
