using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Timers;
using Avalonia.Threading;
using MyAvaloniaApp.Models;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly NotificationService _notificationService;
        private readonly AuthenticationService _authService;
        private readonly Timer _deadlineCheckTimer;
        
        private TaskItem? _selectedTask;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTimeOffset _deadline = DateTimeOffset.Now.AddDays(1);
        private DateTimeOffset? _deadlineDate = DateTimeOffset.Now.Date.AddDays(1);
        private TimeSpan? _deadlineTime = new TimeSpan(9, 0, 0); // Default to 9:00 AM
        private int _selectedStatus = 0;
        private string _statusFilter = "Tất cả";
        private DateTime _currentWeekStart = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);

        public ObservableCollection<TaskItem> Tasks { get; } = new();
        public ObservableCollection<TaskItem> FilteredTasks { get; } = new();
        public ObservableCollection<NotificationItem> Notifications { get; }
        public ObservableCollection<string> StatusFilterOptions { get; } = new()
        {
            "Tất cả", "Chưa làm", "Đang làm", "Hoàn thành"
        };

        public ICommand AddTaskCommand { get; }
        public ICommand UpdateTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand RemoveNotificationCommand { get; }
        public ICommand CheckDeadlinesCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenUserManagementCommand { get; }
        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }
        public ICommand ResetTaskIdCommand { get; }

        public string CurrentUserName => _authService.CurrentUser?.Username ?? "Guest";
        public bool IsAuthenticated => _authService.IsAuthenticated;
        public bool IsAdmin => _authService.IsAdmin;
        public bool HasNotifications => Notifications.Any();

        public MainWindowViewModel()
        {
            _databaseService = DatabaseService.Instance;
            _notificationService = NotificationService.Instance;
            _authService = AuthenticationService.Instance;
            Notifications = _notificationService.Notifications;
            
            // Subscribe to notifications changes to update HasNotifications
            Notifications.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNotifications));

            // Subscribe to authentication events
            _authService.UserLoggedIn += OnUserLoggedIn;
            _authService.UserLoggedOut += OnUserLoggedOut;

            // Commands
            AddTaskCommand = new MainRelayCommand(async () => await AddTaskAsync());
            UpdateTaskCommand = new MainRelayCommand(async () => await UpdateTaskAsync(), () => SelectedTask != null);
            DeleteTaskCommand = new MainRelayCommand(async () => await DeleteTaskAsync(), () => SelectedTask != null);
            ClearFormCommand = new MainRelayCommand(ClearForm);
            RemoveNotificationCommand = new MainRelayCommand<NotificationItem>(RemoveNotification);
            CheckDeadlinesCommand = new MainRelayCommand(async () => await CheckDeadlinesAsync());
            LogoutCommand = new MainRelayCommand(Logout);
            OpenUserManagementCommand = new MainRelayCommand(OpenUserManagement);
            PreviousWeekCommand = new MainRelayCommand(PreviousWeek);
            NextWeekCommand = new MainRelayCommand(NextWeek);
            ResetTaskIdCommand = new MainRelayCommand(async () => await ResetTaskIdAsync());

            // Timer để kiểm tra deadline định kỳ (mỗi 15 phút)
            _deadlineCheckTimer = new Timer(15 * 60 * 1000); // 15 minutes
            _deadlineCheckTimer.Elapsed += async (_, _) => await CheckDeadlinesAsync();
            _deadlineCheckTimer.AutoReset = true;
            _deadlineCheckTimer.Start();

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

            // Load initial data
            Task.Run(async () =>
            {
                await LoadTasksAsync();
                await CheckDeadlinesAsync();
            });

            // Show welcome notification
            _notificationService.ShowSuccess("Chào mừng!", "Ứng dụng quản lý task đã sẵn sàng");
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
                        
                        // Thông báo các command về sự thay đổi CanExecute
                        RaiseCanExecuteChanged();
                    }
                    catch (Exception ex)
                    {
                        _notificationService?.ShowError("Lỗi", $"Lỗi khi chọn task: {ex.Message}");
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

        #region Methods

        private async Task LoadTasksAsync()
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
                _notificationService.ShowError("Lỗi", $"Không thể tải danh sách task: {ex.Message}");
            }
        }

        private void FilterTasks()
        {
            FilteredTasks.Clear();
            
            var filtered = StatusFilter switch
            {
                "Chưa làm" => Tasks.Where(t => t.Status == TaskItemStatus.NotStarted),
                "Đang làm" => Tasks.Where(t => t.Status == TaskItemStatus.InProgress),
                "Hoàn thành" => Tasks.Where(t => t.Status == TaskItemStatus.Completed),
                _ => Tasks
            };

            foreach (var task in filtered)
            {
                FilteredTasks.Add(task);
            }
            
            // Ensure UI knows about the change
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

        private async Task AddTaskAsync()
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

                _notificationService.ShowSuccess("Thành công", $"Đã thêm task '{newTask.Title}'");
                
                // Force refresh calendar display
                OnPropertyChanged(nameof(FilteredTasks));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Lỗi", $"Không thể thêm task: {ex.Message}");
            }
        }

        private async Task UpdateTaskAsync()
        {
            if (SelectedTask == null || string.IsNullOrWhiteSpace(Title))
            {
                _notificationService.ShowWarning("Thông báo", "Vui lòng chọn task và nhập tên công việc");
                return;
            }

            try
            {
                var taskTitle = Title; // Lưu title trước khi cập nhật
                SelectedTask.Title = Title;
                SelectedTask.Description = Description;
                SelectedTask.Deadline = Deadline.DateTime;
                SelectedTask.Status = (TaskItemStatus)SelectedStatus;

                await _databaseService.UpdateTaskAsync(SelectedTask);
                
                // Refresh filtered tasks instead of reloading everything
                FilterTasks();

                _notificationService.ShowSuccess("Thành công", $"Đã cập nhật task '{taskTitle}'");
                
                // Force refresh calendar display
                OnPropertyChanged(nameof(FilteredTasks));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Lỗi", $"Không thể cập nhật task: {ex.Message}");
            }
        }

        private async Task DeleteTaskAsync()
        {
            if (SelectedTask == null) return;

            try
            {
                var taskTitle = SelectedTask.Title; // Lưu title trước khi xóa
                await _databaseService.DeleteTaskAsync(SelectedTask.Id);
                Tasks.Remove(SelectedTask);
                FilterTasks();
                ClearForm();

                _notificationService.ShowSuccess("Thành công", $"Đã xóa task '{taskTitle}'");
                
                // Force refresh calendar display
                OnPropertyChanged(nameof(FilteredTasks));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Lỗi", $"Không thể xóa task: {ex.Message}");
            }
        }

        private void ClearForm()
        {
            Title = string.Empty;
            Description = string.Empty;
            Deadline = DateTimeOffset.Now.AddDays(1);
            DeadlineDate = DateTimeOffset.Now.Date.AddDays(1);
            DeadlineTime = new TimeSpan(9, 0, 0); // Default to 9:00 AM
            SelectedStatus = 0;
            SelectedTask = null;
        }

        private void RaiseCanExecuteChanged()
        {
            try
            {
                (UpdateTaskCommand as MainRelayCommand)?.RaiseCanExecuteChanged();
                (DeleteTaskCommand as MainRelayCommand)?.RaiseCanExecuteChanged();
            }
            catch
            {
                // Ignore errors in command updates
            }
        }

        private void RemoveNotification(NotificationItem? notification)
        {
            if (notification != null)
            {
                _notificationService.RemoveNotification(notification);
            }
        }

        private async Task CheckDeadlinesAsync()
        {
            await _notificationService.CheckTaskDeadlinesAsync();
        }

        private void Logout()
        {
            _authService.Logout();
            OnPropertyChanged(nameof(CurrentUserName));
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsAdmin));
        }

        private void OpenUserManagement()
        {
            if (!IsAdmin)
            {
                _notificationService.ShowWarning("Không có quyền", "Chỉ Admin mới có thể truy cập tính năng này!");
                return;
            }

            var userManagementWindow = new Views.UserManagementWindow();
            userManagementWindow.Show();
        }

        public void RefreshUserInfo()
        {
            OnPropertyChanged(nameof(CurrentUserName));
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsAdmin));
        }

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

        #region Authentication Event Handlers

        private async void OnUserLoggedIn(object? sender, User user)
        {
            // Cập nhật thông tin user và reload tasks cho user mới
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAdmin));
            });

            // Load tasks cho user mới
            await LoadTasksAsync();
            await CheckDeadlinesAsync();
        }

        private async void OnUserLoggedOut(object? sender, EventArgs e)
        {
            // Clear tasks khi đăng xuất
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Tasks.Clear();
                FilterTasks();
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAdmin));
            });
        }

        #endregion

        #region Calendar Properties and Methods

        public DateTime GetCurrentWeekStart() => _currentWeekStart;

        public string CurrentWeekDisplay
        {
            get
            {
                var endWeek = _currentWeekStart.AddDays(6);
                return $"{_currentWeekStart:dd/MM/yyyy} - {endWeek:dd/MM/yyyy}";
            }
        }

        public string SundayDate => _currentWeekStart.Day.ToString();
        public string MondayDate => _currentWeekStart.AddDays(1).Day.ToString();
        public string TuesdayDate => _currentWeekStart.AddDays(2).Day.ToString();
        public string WednesdayDate => _currentWeekStart.AddDays(3).Day.ToString();
        public string ThursdayDate => _currentWeekStart.AddDays(4).Day.ToString();
        public string FridayDate => _currentWeekStart.AddDays(5).Day.ToString();
        public string SaturdayDate => _currentWeekStart.AddDays(6).Day.ToString();

        // Today highlighting properties
        public bool IsSundayToday => DateTime.Today == _currentWeekStart.Date;
        public bool IsMondayToday => DateTime.Today == _currentWeekStart.AddDays(1).Date;
        public bool IsTuesdayToday => DateTime.Today == _currentWeekStart.AddDays(2).Date;
        public bool IsWednesdayToday => DateTime.Today == _currentWeekStart.AddDays(3).Date;
        public bool IsThursdayToday => DateTime.Today == _currentWeekStart.AddDays(4).Date;
        public bool IsFridayToday => DateTime.Today == _currentWeekStart.AddDays(5).Date;
        public bool IsSaturdayToday => DateTime.Today == _currentWeekStart.AddDays(6).Date;

        private void PreviousWeek()
        {
            _currentWeekStart = _currentWeekStart.AddDays(-7);
            RefreshCalendarProperties();
        }

        private void NextWeek()
        {
            _currentWeekStart = _currentWeekStart.AddDays(7);
            RefreshCalendarProperties();
        }

        private void RefreshCalendarProperties()
        {
            OnPropertyChanged(nameof(CurrentWeekDisplay));
            OnPropertyChanged(nameof(SundayDate));
            OnPropertyChanged(nameof(MondayDate));
            OnPropertyChanged(nameof(TuesdayDate));
            OnPropertyChanged(nameof(WednesdayDate));
            OnPropertyChanged(nameof(ThursdayDate));
            OnPropertyChanged(nameof(FridayDate));
            OnPropertyChanged(nameof(SaturdayDate));
            OnPropertyChanged(nameof(IsSundayToday));
            OnPropertyChanged(nameof(IsMondayToday));
            OnPropertyChanged(nameof(IsTuesdayToday));
            OnPropertyChanged(nameof(IsWednesdayToday));
            OnPropertyChanged(nameof(IsThursdayToday));
            OnPropertyChanged(nameof(IsFridayToday));
            OnPropertyChanged(nameof(IsSaturdayToday));
            OnPropertyChanged("CurrentWeekStart"); // For CalendarView
        }

        private async void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is TaskItem task && e.PropertyName == nameof(TaskItem.Status))
            {
                try
                {
                    // Update the task in database
                    await _databaseService.UpdateTaskAsync(task);
                    
                    // Refresh the filtered tasks
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        FilterTasks();
                    });
                    
                    _notificationService.ShowSuccess("Cập nhật", "Trạng thái task đã được cập nhật!");
                }
                catch (Exception ex)
                {
                    _notificationService.ShowError("Lỗi", $"Không thể cập nhật trạng thái: {ex.Message}");
                }
            }
        }

        #endregion

        #region Reset ID Methods

        // Method để reset task ID sequence về 1 mà KHÔNG mất dữ liệu
        private async Task ResetTaskIdAsync()
        {
            try
            {
                // Hiển thị thông báo xác nhận
                _notificationService.ShowInfo("Đang xử lý...", "Đang reset ID sequence. Vui lòng đợi...");
                
                await _databaseService.ResetTaskIdSequencePreserveDataAsync();
                await LoadTasksAsync();
                
                _notificationService.ShowSuccess("Thành công!", 
                    "ID sequence đã được reset về 1. Tất cả dữ liệu được giữ nguyên!");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Lỗi!", 
                    $"Không thể reset ID sequence: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _deadlineCheckTimer?.Stop();
            _deadlineCheckTimer?.Dispose();
            
            // Unsubscribe from authentication events
            _authService.UserLoggedIn -= OnUserLoggedIn;
            _authService.UserLoggedOut -= OnUserLoggedOut;
        }

        #endregion
    }

    // Simple RelayCommand implementation for MainWindow
    public class MainRelayCommand : ICommand
    {
        private readonly Func<Task>? _executeAsync;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;

        public MainRelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public MainRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class MainRelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public MainRelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
