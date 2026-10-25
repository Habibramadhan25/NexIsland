using System;
using System.Collections.Generic;
using System.Linq;

namespace NexIsland.Services
{
    public enum NotificationPriority
    {
        Low,
        Normal,
        High
    }

    public class IslandNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SourceApp { get; set; } = "NexIsland";
        public string IconEmoji { get; set; } = "🔔";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        public bool IsRead { get; set; }

        public string DisplayMessage(bool privacyMode) => privacyMode ? "Notification received" : Message;
    }

    public class NotificationService
    {
        private static NotificationService? _instance;
        public static NotificationService Instance => _instance ??= new NotificationService();

        private readonly List<IslandNotification> _history = new();
        private const int MaxHistory = 50;

        public IReadOnlyList<IslandNotification> History => _history;
        public event Action<IslandNotification>? NotificationReceived;
        public event Action? HistoryChanged;

        public void PostNotification(string title, string message, string source = "NexIsland", string icon = "🔔", NotificationPriority priority = NotificationPriority.Normal)
        {
            var notif = new IslandNotification
            {
                Title = title,
                Message = message,
                SourceApp = source,
                IconEmoji = icon,
                Priority = priority,
                Timestamp = DateTime.Now
            };

            _history.Insert(0, notif);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            NotificationReceived?.Invoke(notif);
            HistoryChanged?.Invoke();
        }

        public void Dismiss(string id)
        {
            var item = _history.FirstOrDefault(n => n.Id == id);
            if (item != null)
            {
                _history.Remove(item);
                HistoryChanged?.Invoke();
            }
        }

        public void ClearAll()
        {
            _history.Clear();
            HistoryChanged?.Invoke();
        }
    }
}
