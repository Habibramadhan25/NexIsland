using System;
using System.Collections.Generic;
using System.Linq;

namespace NexIsland.Services
{
    public class ActivityTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ProgressPercent { get; set; } // 0-100
        public bool IsIndeterminate { get; set; }
        public bool IsCompleted { get; set; }
        public string IconEmoji { get; set; } = "⚡";
        public Action? OnCancel { get; set; }
        public Action? OnOpenLocation { get; set; }
    }

    public class SmartActivityService
    {
        private static SmartActivityService? _instance;
        public static SmartActivityService Instance => _instance ??= new SmartActivityService();

        private readonly List<ActivityTask> _activeTasks = new();

        public IReadOnlyList<ActivityTask> ActiveTasks => _activeTasks;
        public ActivityTask? PrimaryTask => _activeTasks.FirstOrDefault(t => !t.IsCompleted);

        public event Action? TasksChanged;

        public ActivityTask RegisterTask(string title, string description, string emoji = "⚡", Action? onCancel = null)
        {
            var task = new ActivityTask
            {
                Title = title,
                Description = description,
                IconEmoji = emoji,
                OnCancel = onCancel
            };

            _activeTasks.Add(task);
            TasksChanged?.Invoke();
            return task;
        }

        public void UpdateProgress(string taskId, double progressPercent, string? description = null)
        {
            var task = _activeTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
                if (description != null) task.Description = description;
                if (task.ProgressPercent >= 100) task.IsCompleted = true;
                TasksChanged?.Invoke();
            }
        }

        public void CompleteTask(string taskId)
        {
            var task = _activeTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.IsCompleted = true;
                task.ProgressPercent = 100;
                _activeTasks.Remove(task);
                TasksChanged?.Invoke();
            }
        }

        public void CancelTask(string taskId)
        {
            var task = _activeTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.OnCancel?.Invoke();
                _activeTasks.Remove(task);
                TasksChanged?.Invoke();
            }
        }
    }
}
