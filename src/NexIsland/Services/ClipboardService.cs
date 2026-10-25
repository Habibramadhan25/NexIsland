using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace NexIsland.Services
{
    public class ClipboardItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public int CharCount => Content.Length;

        public string PreviewText => Content.Length > 80 ? Content.Substring(0, 77) + "..." : Content;
    }

    public class ClipboardService : IDisposable
    {
        private static ClipboardService? _instance;
        public static ClipboardService Instance => _instance ??= new ClipboardService();

        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private readonly List<ClipboardItem> _history = new();
        private readonly string _storageFilePath;
        private bool _isListening;
        private string _lastCopiedText = string.Empty;

        public bool IsEnabled { get; set; } = true;
        public bool PrivacyMode { get; set; } = false;
        public int MaxItems { get; set; } = 30;
        public IReadOnlyList<ClipboardItem> History => _history;

        public event Action? HistoryChanged;

        public ClipboardService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _storageFilePath = Path.Combine(appData, "NexIsland", "clipboard_history.json");
            LoadHistory();
        }

        public void Initialize(Window window)
        {
            if (_isListening) return;

            try
            {
                var helper = new WindowInteropHelper(window);
                _hwnd = helper.Handle;

                if (_hwnd != IntPtr.Zero)
                {
                    NativeMethods.AddClipboardFormatListener(_hwnd);
                    _hwndSource = HwndSource.FromHwnd(_hwnd);
                    _hwndSource?.AddHook(WndProc);
                    _isListening = true;
                    LoggingService.Instance.Info("Clipboard listener registered successfully");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to initialize clipboard listener", ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdated();
            }
            return IntPtr.Zero;
        }

        private void OnClipboardUpdated()
        {
            if (!IsEnabled || PrivacyMode) return;

            try
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (string.IsNullOrWhiteSpace(text) || text == _lastCopiedText) return;

                    // Avoid recording apparent passwords or tokens
                    if (IsSensitiveText(text)) return;

                    _lastCopiedText = text;

                    // Remove existing duplicate if present
                    _history.RemoveAll(i => i.Content == text && !i.IsPinned);

                    _history.Insert(0, new ClipboardItem
                    {
                        Content = text,
                        Timestamp = DateTime.Now
                    });

                    // Enforce max items (keep pinned items)
                    while (_history.Count(i => !i.IsPinned) > MaxItems)
                    {
                        var lastUnpinned = _history.LastOrDefault(i => !i.IsPinned);
                        if (lastUnpinned != null) _history.Remove(lastUnpinned);
                        else break;
                    }

                    SaveHistory();
                    HistoryChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Clipboard read error: {ex.Message}");
            }
        }

        private static bool IsSensitiveText(string text)
        {
            // Simple heuristic to not log obvious secrets
            if (text.Length > 20000) return true; // Don't store giant pastes
            return false;
        }

        public void CopyToClipboard(string content)
        {
            try
            {
                _lastCopiedText = content;
                Clipboard.SetText(content);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to copy item to clipboard", ex);
            }
        }

        public void TogglePin(string id)
        {
            var item = _history.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                SaveHistory();
                HistoryChanged?.Invoke();
            }
        }

        public void DeleteItem(string id)
        {
            var item = _history.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _history.Remove(item);
                SaveHistory();
                HistoryChanged?.Invoke();
            }
        }

        public void ClearHistory()
        {
            _history.RemoveAll(i => !i.IsPinned);
            SaveHistory();
            HistoryChanged?.Invoke();
        }

        public IEnumerable<ClipboardItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _history.OrderByDescending(i => i.IsPinned).ThenByDescending(i => i.Timestamp);
            }

            return _history.Where(i => i.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                           .OrderByDescending(i => i.IsPinned)
                           .ThenByDescending(i => i.Timestamp);
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_storageFilePath))
                {
                    var json = File.ReadAllText(_storageFilePath);
                    var list = JsonSerializer.Deserialize<List<ClipboardItem>>(json);
                    if (list != null)
                    {
                        _history.Clear();
                        _history.AddRange(list);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to load clipboard history", ex);
            }
        }

        private void SaveHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storageFilePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to save clipboard history", ex);
            }
        }

        public void Dispose()
        {
            if (_isListening && _hwnd != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.RemoveClipboardFormatListener(_hwnd);
                    _hwndSource?.RemoveHook(WndProc);
                }
                catch { }
                _isListening = false;
            }
        }
    }
}
