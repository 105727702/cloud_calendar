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
    public partial class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Services
        private readonly DatabaseService _databaseService;
        private readonly NotificationService _notificationService;
        private readonly AuthenticationService _authService;
        private readonly Timer _deadlineCheckTimer;
        #endregion

        #region Managers
        public TaskManager TaskManager { get; }
        public CalendarManager CalendarManager { get; }
        #endregion

        #region Notification Properties
        public ObservableCollection<NotificationItem> Notifications { get; }
        public bool HasNotifications => Notifications.Any();
        #endregion

        #region Commands
        public ICommand AddTaskCommand { get; }
        public ICommand UpdateTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand ResetTaskIdCommand { get; }
        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenUserManagementCommand { get; }
        public ICommand RemoveNotificationCommand { get; private set; } = null!;
        public ICommand CheckDeadlinesCommand { get; private set; } = null!;
        #endregion

        #region Authentication Properties
        public string CurrentUserName => _authService.CurrentUser?.Username ?? "Guest";
        public bool IsAuthenticated => _authService.IsAuthenticated;
        public bool IsAdmin => _authService.IsAdmin;
        #endregion

        #region Delegated Properties (để giữ tương thích với XAML binding)
        
        // Task properties
        public ObservableCollection<TaskItem> Tasks => TaskManager.Tasks;
        public ObservableCollection<TaskItem> FilteredTasks => TaskManager.FilteredTasks;
        public ObservableCollection<string> StatusFilterOptions => TaskManager.StatusFilterOptions;
        public TaskItem? SelectedTask 
        { 
            get => TaskManager.SelectedTask; 
            set => TaskManager.SelectedTask = value; 
        }
        public string Title 
        { 
            get => TaskManager.Title; 
            set => TaskManager.Title = value; 
        }
        public string Description 
        { 
            get => TaskManager.Description; 
            set => TaskManager.Description = value; 
        }
        public DateTimeOffset Deadline 
        { 
            get => TaskManager.Deadline; 
            set => TaskManager.Deadline = value; 
        }
        public DateTimeOffset? DeadlineDate 
        { 
            get => TaskManager.DeadlineDate; 
            set => TaskManager.DeadlineDate = value; 
        }
        public TimeSpan? DeadlineTime 
        { 
            get => TaskManager.DeadlineTime; 
            set => TaskManager.DeadlineTime = value; 
        }
        public int SelectedStatus 
        { 
            get => TaskManager.SelectedStatus; 
            set => TaskManager.SelectedStatus = value; 
        }
        public string StatusFilter 
        { 
            get => TaskManager.StatusFilter; 
            set => TaskManager.StatusFilter = value; 
        }

        // Calendar properties
        public DateTime GetCurrentWeekStart() => CalendarManager.GetCurrentWeekStart();
        public string CurrentWeekDisplay => CalendarManager.CurrentWeekDisplay;
        public string SundayDate => CalendarManager.SundayDate;
        public string MondayDate => CalendarManager.MondayDate;
        public string TuesdayDate => CalendarManager.TuesdayDate;
        public string WednesdayDate => CalendarManager.WednesdayDate;
        public string ThursdayDate => CalendarManager.ThursdayDate;
        public string FridayDate => CalendarManager.FridayDate;
        public string SaturdayDate => CalendarManager.SaturdayDate;
        public bool IsSundayToday => CalendarManager.IsSundayToday;
        public bool IsMondayToday => CalendarManager.IsMondayToday;
        public bool IsTuesdayToday => CalendarManager.IsTuesdayToday;
        public bool IsWednesdayToday => CalendarManager.IsWednesdayToday;
        public bool IsThursdayToday => CalendarManager.IsThursdayToday;
        public bool IsFridayToday => CalendarManager.IsFridayToday;
        public bool IsSaturdayToday => CalendarManager.IsSaturdayToday;

        #endregion

        public MainWindowViewModel()
        {
            _databaseService = DatabaseService.Instance;
            _notificationService = NotificationService.Instance;
            _authService = AuthenticationService.Instance;
            Notifications = _notificationService.Notifications;
            
            // Initialize managers
            TaskManager = new TaskManager(_databaseService, _notificationService, _authService);
            CalendarManager = new CalendarManager();
            
            // Subscribe to property changes from managers
            TaskManager.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            CalendarManager.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            TaskManager.CanExecuteChanged += () => RaiseCanExecuteChanged();
            
            // Subscribe to notifications changes to update HasNotifications
            Notifications.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNotifications));

            // Initialize commands
            AddTaskCommand = new MainRelayCommand(async () => await TaskManager.AddTaskAsync());
            UpdateTaskCommand = new MainRelayCommand(async () => await TaskManager.UpdateTaskAsync(), () => TaskManager.CanUpdateTask());
            DeleteTaskCommand = new MainRelayCommand(async () => await TaskManager.DeleteTaskAsync(), () => TaskManager.CanDeleteTask());
            ClearFormCommand = new MainRelayCommand(TaskManager.ClearForm);
            ResetTaskIdCommand = new MainRelayCommand(async () => await TaskManager.ResetTaskIdAsync());
            PreviousWeekCommand = new MainRelayCommand(CalendarManager.PreviousWeek);
            NextWeekCommand = new MainRelayCommand(CalendarManager.NextWeek);
            LogoutCommand = new MainRelayCommand(Logout);
            OpenUserManagementCommand = new MainRelayCommand(OpenUserManagement);
            RemoveNotificationCommand = new MainRelayCommand<NotificationItem>(RemoveNotification);
            CheckDeadlinesCommand = new MainRelayCommand(async () => await CheckDeadlinesAsync());

            // Initialize authentication events
            _authService.UserLoggedIn += OnUserLoggedIn;
            _authService.UserLoggedOut += OnUserLoggedOut;

            // Timer để kiểm tra deadline định kỳ (mỗi 15 phút)
            _deadlineCheckTimer = new Timer(15 * 60 * 1000); // 15 minutes
            _deadlineCheckTimer.Elapsed += async (_, _) => await CheckDeadlinesAsync();
            _deadlineCheckTimer.AutoReset = true;
            _deadlineCheckTimer.Start();

            // Load initial data
            Task.Run(async () =>
            {
                await TaskManager.LoadTasksAsync();
                await CheckDeadlinesAsync();
            });

            // Show welcome notification
            _notificationService.ShowSuccess("Chào mừng!", "Ứng dụng quản lý task đã sẵn sàng");
        }

        #region Methods

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

        private async void OnUserLoggedIn(object? sender, User user)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAdmin));
            });

            await TaskManager.LoadTasksAsync();
            await CheckDeadlinesAsync();
        }

        private async void OnUserLoggedOut(object? sender, EventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TaskManager.Tasks.Clear();
                TaskManager.FilterTasks();
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAdmin));
            });
        }

        #endregion

        #region Notification Methods

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
}
