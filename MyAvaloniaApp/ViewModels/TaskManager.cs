using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MyAvaloniaApp.Models;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class TaskManager : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly NotificationService _notificationService;
        private readonly AuthenticationService _authService;
        
        private TaskItem? _selectedTask;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTimeOffset _deadline = DateTimeOffset.Now.AddDays(1);
        private DateTimeOffset? _deadlineDate = DateTimeOffset.Now.Date.AddDays(1);
        private TimeSpan? _deadlineTime = new TimeSpan(9, 0, 0);
        private int _selectedStatus = 0;
        private string _statusFilter = "All";

        public ObservableCollection<TaskItem> Tasks { get; } = new();
        public ObservableCollection<TaskItem> FilteredTasks { get; } = new();
        public ObservableCollection<string> StatusFilterOptions { get; } = new()
        {
            "All", "Not Started", "In Progress", "Completed"
        };

        public TaskManager(DatabaseService databaseService, NotificationService notificationService, AuthenticationService authService)
        {
            _databaseService = databaseService;
            _notificationService = notificationService;
            _authService = authService;

            // Subscribe to Tasks collection changes to monitor status updates
            Tasks.CollectionChanged += (sender, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (TaskItem item in e.NewItems)
                    {
                        item.PropertyChanged += OnTaskPropertyChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (TaskItem item in e.OldItems)
                    {
                        item.PropertyChanged -= OnTaskPropertyChanged;
                    }
                }
            };
        }

        #region Properties

        public TaskItem? SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (SetProperty(ref _selectedTask, value))
                {
                    try
                    {
                        if (value != null)
                        {
                            Title = value.Title ?? string.Empty;
                            Description = value.Description ?? string.Empty;
                            Deadline = new DateTimeOffset(value.Deadline);
                            DeadlineDate = new DateTimeOffset(value.Deadline.Date);
                            DeadlineTime = value.Deadline.TimeOfDay;
                            SelectedStatus = (int)value.Status;
                        }
                        
                        CanExecuteChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _notificationService?.ShowError("Error", $"Error when selecting task: {ex.Message}");
                    }
                }
            }
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public DateTimeOffset Deadline
        {
            get => _deadline;
            set => SetProperty(ref _deadline, value);
        }

        public DateTimeOffset? DeadlineDate
        {
            get => _deadlineDate;
            set 
            { 
                if (SetProperty(ref _deadlineDate, value))
                {
                    UpdateDeadlineFromComponents();
                }
            }
        }

        public TimeSpan? DeadlineTime
        {
            get => _deadlineTime;
            set 
            { 
                if (SetProperty(ref _deadlineTime, value))
                {
                    UpdateDeadlineFromComponents();
                }
            }
        }

        public int SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    FilterTasks();
                }
            }
        }

        #endregion

        #region Events
        public event Action? CanExecuteChanged;
        #endregion

        #region Methods

        public async Task LoadTasksAsync()
        {
            try
            {
                var userId = _authService.CurrentUser?.Id;
                var tasks = await _databaseService.GetAllTasksAsync(userId);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Tasks.Clear();
                    foreach (var task in tasks)
                    {
                        Tasks.Add(task);
                    }
                    FilterTasks();
                });
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error", $"Unable to load task list: {ex.Message}");
            }
        }

        public void FilterTasks()
        {
            FilteredTasks.Clear();
            
            var filtered = StatusFilter switch
            {
                "Not Started" => Tasks.Where(t => t.Status == TaskItemStatus.NotStarted),
                "In Progress" => Tasks.Where(t => t.Status == TaskItemStatus.InProgress),
                "Completed" => Tasks.Where(t => t.Status == TaskItemStatus.Completed),
                _ => Tasks
            };

            foreach (var task in filtered)
            {
                FilteredTasks.Add(task);
            }
            
            OnPropertyChanged(nameof(FilteredTasks));
        }

        private void UpdateDeadlineFromComponents()
        {
            if (DeadlineDate.HasValue && DeadlineTime.HasValue)
            {
                var date = DeadlineDate.Value.Date;
                var time = DeadlineTime.Value;
                _deadline = new DateTimeOffset(date.Add(time));
                OnPropertyChanged(nameof(Deadline));
            }
        }

        public async Task AddTaskAsync()
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                _notificationService.ShowWarning("Thông báo", "Vui lòng nhập tên công việc");
                return;
            }

            try
            {
                var newTask = new TaskItem
                {
                    Title = Title,
                    Description = Description,
                    Deadline = Deadline.DateTime,
                    Status = (TaskItemStatus)SelectedStatus
                };

                var userId = _authService.CurrentUser?.Id;
                var id = await _databaseService.AddTaskAsync(newTask, userId);
                newTask.Id = id;

                Tasks.Add(newTask);
                FilterTasks();
                ClearForm();

                _notificationService.ShowSuccess("Success", $"Added task '{newTask.Title}'");
                OnPropertyChanged(nameof(FilteredTasks));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error", $"Unable to add task: {ex.Message}");
            }
        }

        public async Task UpdateTaskAsync()
        {
            if (SelectedTask == null || string.IsNullOrWhiteSpace(Title))
            {
                _notificationService.ShowWarning("Thông báo", "Vui lòng chọn task và nhập tên công việc");
                return;
            }

            try
            {
                var taskTitle = Title;
                SelectedTask.Title = Title;
                SelectedTask.Description = Description;
                SelectedTask.Deadline = Deadline.DateTime;
                SelectedTask.Status = (TaskItemStatus)SelectedStatus;

                await _databaseService.UpdateTaskAsync(SelectedTask);
                FilterTasks();

                _notificationService.ShowSuccess("Success", $"Updated task '{taskTitle}'");
                OnPropertyChanged(nameof(FilteredTasks));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error", $"Unable to update task: {ex.Message}");
            }
        }

        public async Task DeleteTaskAsync()
        {
            if (SelectedTask == null) return;

            try
            {
                var taskTitle = SelectedTask.Title;
                await _databaseService.DeleteTaskAsync(SelectedTask.Id);
                Tasks.Remove(SelectedTask);
                FilterTasks();
                ClearForm();

                _notificationService.ShowSuccess("Success", $"Deleted task '{taskTitle}'");
                OnPropertyChanged(nameof(FilteredTasks));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error", $"Unable to delete task: {ex.Message}");
            }
        }

        public void ClearForm()
        {
            Title = string.Empty;
            Description = string.Empty;
            Deadline = DateTimeOffset.Now.AddDays(1);
            DeadlineDate = DateTimeOffset.Now.Date.AddDays(1);
            DeadlineTime = new TimeSpan(9, 0, 0);
            SelectedStatus = 0;
            SelectedTask = null;
        }

        public async Task ResetTaskIdAsync()
        {
            try
            {
                _notificationService.ShowInfo("Processing...", "Resetting ID sequence. Please wait...");
                
                await _databaseService.ResetTaskIdSequencePreserveDataAsync();
                await LoadTasksAsync();
                
                _notificationService.ShowSuccess("Success!", 
                    "ID sequence has been reset to 1. All data has been preserved!");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error!", 
                    $"Unable to reset ID sequence: {ex.Message}");
            }
        }

        private async void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is TaskItem task && e.PropertyName == nameof(TaskItem.Status))
            {
                try
                {
                    await _databaseService.UpdateTaskAsync(task);
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        FilterTasks();
                    });
                    
                    _notificationService.ShowSuccess("Update", "Task status has been updated!");
                }
                catch (Exception ex)
                {
                    _notificationService.ShowError("Error", $"Unable to update status: {ex.Message}");
                }
            }
        }

        public bool CanUpdateTask() => SelectedTask != null;
        public bool CanDeleteTask() => SelectedTask != null;

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
