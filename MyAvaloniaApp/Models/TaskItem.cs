using System;
using ReactiveUI;

namespace MyAvaloniaApp.Models
{
    public enum TaskItemStatus
    {
        NotStarted,
        InProgress,
        Completed
    }

    public class TaskItem : ReactiveObject
    {
        private int _id;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTime _deadline;
        private TaskItemStatus _status;

        public int Id
        {
            get => _id;
            set => this.RaiseAndSetIfChanged(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public DateTime Deadline
        {
            get => _deadline;
            set => this.RaiseAndSetIfChanged(ref _deadline, value);
        }

        public TaskItemStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public string StatusText => Status switch
        {
            TaskItemStatus.NotStarted => "Chưa làm",
            TaskItemStatus.InProgress => "Đang làm",
            TaskItemStatus.Completed => "Hoàn thành",
            _ => "Không xác định"
        };

        public bool IsOverdue => DateTime.Now > Deadline && Status != TaskItemStatus.Completed;

        public bool IsNearDeadline => (Deadline - DateTime.Now).TotalHours <= 24 && Status != TaskItemStatus.Completed;
    }
}
