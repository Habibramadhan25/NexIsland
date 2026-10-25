using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public enum FocusModeType
    {
        Pomodoro,
        CustomTimer,
        Stopwatch,
        Countdown
    }

    public enum PomodoroStage
    {
        Work,
        ShortBreak,
        LongBreak
    }

    public class FocusSessionLog
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int DurationMinutes { get; set; }
        public string Mode { get; set; } = "Pomodoro";
        public bool Completed { get; set; } = true;
    }

    public class FocusTimerService
    {
        private static FocusTimerService? _instance;
        public static FocusTimerService Instance => _instance ??= new FocusTimerService();

        private readonly DispatcherTimer _timer;
        private DateTime _targetEndTime;
        private DateTime _sessionStartTime;
        private TimeSpan _remainingTime;
        private TimeSpan _initialDuration;
        private bool _isRunning;
        private bool _isPaused;

        // Pomodoro state
        private PomodoroStage _pomodoroStage = PomodoroStage.Work;
        private int _completedPomodoros = 0;
        private readonly List<FocusSessionLog> _history = new();
        private readonly string _historyFilePath;

        public event Action? Tick;
        public event Action<string>? SessionCompleted;
        public event Action<FocusModeType, string>? StateChanged;

        public FocusModeType CurrentMode { get; set; } = FocusModeType.Pomodoro;
        public PomodoroStage CurrentPomodoroStage => _pomodoroStage;
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public TimeSpan RemainingTime => _remainingTime;
        public TimeSpan ElapsedTime => DateTime.UtcNow - _sessionStartTime;
        public double ProgressFraction => _initialDuration.TotalSeconds > 0
            ? Math.Clamp(1.0 - (_remainingTime.TotalSeconds / _initialDuration.TotalSeconds), 0.0, 1.0)
            : 0.0;
        public int CompletedPomodoros => _completedPomodoros;
        public IReadOnlyList<FocusSessionLog> History => _history;

        public FocusTimerService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _historyFilePath = Path.Combine(appData, "NexIsland", "focus_history.json");
            LoadHistory();

            _timer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += OnTick;
        }

        public void StartPomodoro(int workMinutes = 25, int shortBreakMinutes = 5, int longBreakMinutes = 15)
        {
            CurrentMode = FocusModeType.Pomodoro;
            _pomodoroStage = PomodoroStage.Work;
            StartCountdown(TimeSpan.FromMinutes(workMinutes));
            StateChanged?.Invoke(CurrentMode, "Pomodoro Focus Started");
        }

        public void StartCustomTimer(TimeSpan duration)
        {
            CurrentMode = FocusModeType.CustomTimer;
            StartCountdown(duration);
            StateChanged?.Invoke(CurrentMode, $"Custom Timer: {duration.TotalMinutes:0} min");
        }

        public void StartStopwatch()
        {
            CurrentMode = FocusModeType.Stopwatch;
            _sessionStartTime = DateTime.UtcNow;
            _isRunning = true;
            _isPaused = false;
            _remainingTime = TimeSpan.Zero;
            _initialDuration = TimeSpan.Zero;
            _timer.Start();
            StateChanged?.Invoke(CurrentMode, "Stopwatch Started");
        }

        private void StartCountdown(TimeSpan duration)
        {
            _initialDuration = duration;
            _remainingTime = duration;
            _sessionStartTime = DateTime.UtcNow;
            _targetEndTime = _sessionStartTime + duration;
            _isRunning = true;
            _isPaused = false;
            _timer.Start();
        }

        public void Pause()
        {
            if (!_isRunning || _isPaused) return;
            _isPaused = true;
            _timer.Stop();
            StateChanged?.Invoke(CurrentMode, "Paused");
        }

        public void Resume()
        {
            if (!_isRunning || !_isPaused) return;
            _isPaused = false;
            _sessionStartTime = DateTime.UtcNow;
            _targetEndTime = DateTime.UtcNow + _remainingTime;
            _timer.Start();
            StateChanged?.Invoke(CurrentMode, "Resumed");
        }

        public void Reset()
        {
            _isRunning = false;
            _isPaused = false;
            _timer.Stop();
            _remainingTime = TimeSpan.Zero;
            Tick?.Invoke();
            StateChanged?.Invoke(CurrentMode, "Reset");
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (!_isRunning || _isPaused) return;

            if (CurrentMode == FocusModeType.Stopwatch)
            {
                _remainingTime = DateTime.UtcNow - _sessionStartTime;
                Tick?.Invoke();
                return;
            }

            var now = DateTime.UtcNow;
            var remaining = _targetEndTime - now;

            if (remaining <= TimeSpan.Zero)
            {
                _remainingTime = TimeSpan.Zero;
                _isRunning = false;
                _timer.Stop();
                Tick?.Invoke();
                HandleSessionCompletion();
            }
            else
            {
                _remainingTime = remaining;
                Tick?.Invoke();
            }
        }

        private void HandleSessionCompletion()
        {
            string message;
            if (CurrentMode == FocusModeType.Pomodoro)
            {
                if (_pomodoroStage == PomodoroStage.Work)
                {
                    _completedPomodoros++;
                    LogCompletedSession((int)_initialDuration.TotalMinutes, "Pomodoro Work");
                    message = "Pomodoro completed! Time for a short break.";
                    _pomodoroStage = (_completedPomodoros % 4 == 0) ? PomodoroStage.LongBreak : PomodoroStage.ShortBreak;
                }
                else
                {
                    message = "Break finished! Ready to focus again?";
                    _pomodoroStage = PomodoroStage.Work;
                }
            }
            else
            {
                message = "Focus timer completed!";
                LogCompletedSession((int)_initialDuration.TotalMinutes, CurrentMode.ToString());
            }

            SessionCompleted?.Invoke(message);
            StateChanged?.Invoke(CurrentMode, message);
        }

        private void LogCompletedSession(int durationMinutes, string mode)
        {
            try
            {
                _history.Add(new FocusSessionLog
                {
                    DurationMinutes = durationMinutes,
                    Mode = mode,
                    Timestamp = DateTime.Now,
                    Completed = true
                });
                SaveHistory();
            }
            catch { }
        }

        public int GetTodayFocusMinutes()
        {
            var today = DateTime.Today;
            int total = 0;
            foreach (var item in _history)
            {
                if (item.Timestamp.Date == today && item.Completed)
                {
                    total += item.DurationMinutes;
                }
            }
            return total;
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var list = JsonSerializer.Deserialize<List<FocusSessionLog>>(json);
                    if (list != null)
                    {
                        _history.Clear();
                        _history.AddRange(list);
                    }
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyFilePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch { }
        }
    }
}
