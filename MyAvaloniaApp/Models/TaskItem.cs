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
            set 
            { 
                var oldValue = _status;
                this.RaiseAndSetIfChanged(ref _status, value);
                if (oldValue != value)
                {
                    this.RaisePropertyChanged(nameof(StatusText));
                    this.RaisePropertyChanged(nameof(IsOverdue));
                    this.RaisePropertyChanged(nameof(IsNearDeadline));
                }
            }
        }

        public string StatusText => Status switch
        {
            TaskItemStatus.NotStarted => "Not Started",
            TaskItemStatus.InProgress => "In Progress",
            TaskItemStatus.Completed => "Completed",
            _ => "Unknown"
        };

        public bool IsOverdue => DateTime.Now > Deadline && Status != TaskItemStatus.Completed;

        public bool IsNearDeadline => (Deadline - DateTime.Now).TotalHours <= 24 && Status != TaskItemStatus.Completed;
    }
}
