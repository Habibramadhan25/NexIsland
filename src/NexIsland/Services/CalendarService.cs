using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public class ReminderItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsRecurringDaily { get; set; }

        public bool IsOverdue => !IsCompleted && DueDate < DateTime.Now;
    }

    public class CalendarService
    {
        private static CalendarService? _instance;
        public static CalendarService Instance => _instance ??= new CalendarService();

        private readonly DispatcherTimer _timer;
        private readonly List<ReminderItem> _reminders = new();
        private readonly string _storageFilePath;

        public IReadOnlyList<ReminderItem> Reminders => _reminders;
        public event Action? RemindersChanged;
        public event Action<ReminderItem>? ReminderDue;

        public CalendarService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _storageFilePath = Path.Combine(appData, "NexIsland", "reminders.json");
            LoadReminders();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += (s, e) => CheckDueReminders();
            _timer.Start();
        }

        public void AddReminder(string title, DateTime dueDate, bool isRecurring = false)
        {
            _reminders.Add(new ReminderItem
            {
                Title = title,
                DueDate = dueDate,
                IsRecurringDaily = isRecurring,
                IsCompleted = false
            });
            SaveReminders();
            RemindersChanged?.Invoke();
        }

        public void CompleteReminder(string id)
        {
            var item = _reminders.FirstOrDefault(r => r.Id == id);
            if (item != null)
            {
                if (item.IsRecurringDaily)
                {
                    item.DueDate = item.DueDate.AddDays(1);
                }
                else
                {
                    item.IsCompleted = true;
                }
                SaveReminders();
                RemindersChanged?.Invoke();
            }
        }

        public void SnoozeReminder(string id, TimeSpan duration)
        {
            var item = _reminders.FirstOrDefault(r => r.Id == id);
            if (item != null)
            {
                item.DueDate = DateTime.Now.Add(duration);
                item.IsCompleted = false;
                SaveReminders();
                RemindersChanged?.Invoke();
            }
        }

        public void DeleteReminder(string id)
        {
            var item = _reminders.FirstOrDefault(r => r.Id == id);
            if (item != null)
            {
                _reminders.Remove(item);
                SaveReminders();
                RemindersChanged?.Invoke();
            }
        }

        private void CheckDueReminders()
        {
            var now = DateTime.Now;
            foreach (var reminder in _reminders.Where(r => !r.IsCompleted))
            {
                if (now >= reminder.DueDate && now <= reminder.DueDate.AddMinutes(2))
                {
                    ReminderDue?.Invoke(reminder);
                }
            }
        }

        private void LoadReminders()
        {
            try
            {
                if (File.Exists(_storageFilePath))
                {
                    var json = File.ReadAllText(_storageFilePath);
                    var list = JsonSerializer.Deserialize<List<ReminderItem>>(json);
                    if (list != null)
                    {
                        _reminders.Clear();
                        _reminders.AddRange(list);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to load reminders", ex);
            }
        }

        private void SaveReminders()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storageFilePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_reminders, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to save reminders", ex);
            }
        }
    }
}
