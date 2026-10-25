using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NexIsland.Services
{
    public class LyricLine
    {
        public TimeSpan Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class LyricsService
    {
        private static LyricsService? _instance;
        public static LyricsService Instance => _instance ??= new LyricsService();

        private readonly List<LyricLine> _currentLyrics = new();
        private static readonly Regex LrcRegex = new(@"\[(\d{2}):(\d{2})(?:\.(\d{2,3}))?\](.*)", RegexOptions.Compiled);

        public IReadOnlyList<LyricLine> CurrentLyrics => _currentLyrics;
        public string CurrentLineText { get; private set; } = "Ready for music";
        public string PreviousLineText { get; private set; } = string.Empty;
        public string NextLineText { get; private set; } = string.Empty;
        public int CurrentLineIndex { get; private set; } = -1;
        public bool HasLyrics => _currentLyrics.Count > 0;
        public bool KaraokeMode { get; set; } = true;
        public double OffsetSeconds { get; set; } = 0.0;

        public event Action? LyricsUpdated;
        public event Action<string>? ActiveLineChanged;

        public void LoadLrcContent(string lrcText)
        {
            _currentLyrics.Clear();
            CurrentLineIndex = -1;

            if (string.IsNullOrWhiteSpace(lrcText))
            {
                CurrentLineText = "No lyrics available";
                PreviousLineText = string.Empty;
                NextLineText = string.Empty;
                LyricsUpdated?.Invoke();
                return;
            }

            var lines = lrcText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var match = LrcRegex.Match(rawLine);
                if (match.Success)
                {
                    int min = int.Parse(match.Groups[1].Value);
                    int sec = int.Parse(match.Groups[2].Value);
                    int ms = 0;
                    if (match.Groups[3].Success)
                    {
                        var msStr = match.Groups[3].Value;
                        if (msStr.Length == 2) ms = int.Parse(msStr) * 10;
                        else if (msStr.Length == 3) ms = int.Parse(msStr);
                    }

                    var time = new TimeSpan(0, 0, min, sec, ms);
                    var text = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _currentLyrics.Add(new LyricLine { Timestamp = time, Text = text });
                    }
                }
            }

            _currentLyrics.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            if (_currentLyrics.Count > 0)
            {
                CurrentLineText = _currentLyrics[0].Text;
                NextLineText = _currentLyrics.Count > 1 ? _currentLyrics[1].Text : string.Empty;
            }
            else
            {
                CurrentLineText = "Instrumental / No synced lyrics";
                PreviousLineText = string.Empty;
                NextLineText = string.Empty;
            }

            LyricsUpdated?.Invoke();
        }

        public void LoadLrcFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var content = File.ReadAllText(filePath);
                    LoadLrcContent(content);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Failed to load LRC file {filePath}", ex);
            }
        }

        public void UpdatePosition(TimeSpan position)
        {
            if (_currentLyrics.Count == 0) return;

            var targetTime = position + TimeSpan.FromSeconds(OffsetSeconds);

            int newIndex = -1;
            for (int i = 0; i < _currentLyrics.Count; i++)
            {
                if (_currentLyrics[i].Timestamp <= targetTime)
                {
                    newIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (newIndex != CurrentLineIndex)
            {
                CurrentLineIndex = newIndex;
                for (int i = 0; i < _currentLyrics.Count; i++)
                {
                    _currentLyrics[i].IsActive = (i == newIndex);
                }

                if (newIndex >= 0 && newIndex < _currentLyrics.Count)
                {
                    CurrentLineText = _currentLyrics[newIndex].Text;
                    PreviousLineText = newIndex > 0 ? _currentLyrics[newIndex - 1].Text : string.Empty;
                    NextLineText = (newIndex + 1 < _currentLyrics.Count) ? _currentLyrics[newIndex + 1].Text : string.Empty;
                }
                else
                {
                    CurrentLineText = _currentLyrics[0].Text;
                    PreviousLineText = string.Empty;
                    NextLineText = _currentLyrics.Count > 1 ? _currentLyrics[1].Text : string.Empty;
                }

                ActiveLineChanged?.Invoke(CurrentLineText);
                LyricsUpdated?.Invoke();
            }
        }

        public void SetSampleLyricsForTrack(string title, string artist)
        {
            // Provides dynamic synced lyrics demonstration when external LRC is not present
            var sample = $@"[00:00.00] ♪ {title} ♪
[00:03.00] Playing by {artist}
[00:08.00] Experience the ambient rhythm of NexIsland
[00:15.00] Dynamic Island for Windows 10 & 11
[00:22.00] Smooth swipe navigation active
[00:30.00] High fidelity audio & real-time visualizer
[00:40.00] Enjoy your music with live lyrics synchronization
[00:52.00] ♪ Instrumental melody ♪
[01:10.00] Focused work with dynamic ambiance
[01:30.00] ♪ Outro ♪";
            LoadLrcContent(sample);
        }

        public void Clear()
        {
            _currentLyrics.Clear();
            CurrentLineIndex = -1;
            CurrentLineText = "No media playing";
            PreviousLineText = string.Empty;
            NextLineText = string.Empty;
            LyricsUpdated?.Invoke();
        }
    }
}
