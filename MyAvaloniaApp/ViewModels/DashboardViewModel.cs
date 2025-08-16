using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MyAvaloniaApp.Models;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class ChartDataPoint : INotifyPropertyChanged
    {
        private double _totalBarHeight;
        private double _completedBarHeight;

        public string Period { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double CompletionRate { get; set; }
        public DateTime Date { get; set; }

        public double TotalBarHeight
        {
            get => _totalBarHeight;
            set
            {
                _totalBarHeight = value;
                OnPropertyChanged();
            }
        }

        public double CompletedBarHeight
        {
            get => _completedBarHeight;
            set
            {
                _completedBarHeight = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly DashboardService _dashboardService;
        private readonly AuthenticationService _authService;
        private DashboardStatistics? _currentStatistics;
        private bool _isLoading;
        private bool _isDayView = true;
        private bool _isWeekView;
        private bool _isMonthView;

        public DashboardViewModel()
        {
            // Design-time constructor
            var mockTaskRepo = new TaskRepository("Data Source=:memory:");
            _dashboardService = new DashboardService(mockTaskRepo);
            _authService = AuthenticationService.Instance;
            
            ChartData = new ObservableCollection<ChartDataPoint>();
            BackCommand = new RelayCommand(OnBack);
            RefreshCommand = new RelayCommand(async () => await RefreshData());
            
            // Initialize with sample data for design time
            InitializeSampleData();
        }

        public DashboardViewModel(DashboardService dashboardService, AuthenticationService authService)
        {
            _dashboardService = dashboardService;
            _authService = authService;
            
            ChartData = new ObservableCollection<ChartDataPoint>();
            BackCommand = new RelayCommand(OnBack);
            RefreshCommand = new RelayCommand(async () => await RefreshData());
            
            // Load data immediately
            _ = Task.Run(async () => await RefreshData());
            
            // Auto refresh every 30 seconds
            var timer = new System.Timers.Timer(30000);
            timer.Elapsed += async (sender, e) => await RefreshData();
            timer.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? BackRequested;

        public ObservableCollection<ChartDataPoint> ChartData { get; }
        public ICommand BackCommand { get; }
        public ICommand RefreshCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public bool IsDayView
        {
            get => _isDayView;
            set
            {
                if (_isDayView != value)
                {
                    _isDayView = value;
                    OnPropertyChanged();
                    if (value) _ = Task.Run(async () => await RefreshData());
                }
            }
        }

        public bool IsWeekView
        {
            get => _isWeekView;
            set
            {
                if (_isWeekView != value)
                {
                    _isWeekView = value;
                    OnPropertyChanged();
                    if (value) _ = Task.Run(async () => await RefreshData());
                }
            }
        }

        public bool IsMonthView
        {
            get => _isMonthView;
            set
            {
                if (_isMonthView != value)
                {
                    _isMonthView = value;
                    OnPropertyChanged();
                    if (value) _ = Task.Run(async () => await RefreshData());
                }
            }
        }

        public int TotalTasks => _currentStatistics?.TotalTasksInPeriod ?? 0;
        public int CompletedTasks => _currentStatistics?.CompletedTasksInPeriod ?? 0;
        public int PendingTasks => TotalTasks - CompletedTasks;
        public string CompletionRateText => $"{_currentStatistics?.OverallCompletionRate ?? 0:F1}%";

        private async Task RefreshData()
        {
            try
            {
                IsLoading = true;

                var timePeriod = IsDayView ? TimePeriod.Day :
                                IsWeekView ? TimePeriod.Week :
                                TimePeriod.Month;

                var userId = _authService.CurrentUser?.Id;
                System.Diagnostics.Debug.WriteLine($"Refreshing dashboard for user: {userId}, period: {timePeriod}");
                
                _currentStatistics = await _dashboardService.GetDashboardStatisticsAsync(timePeriod, userId);
                
                System.Diagnostics.Debug.WriteLine($"Dashboard data: Total={_currentStatistics.TotalTasksInPeriod}, Completed={_currentStatistics.CompletedTasksInPeriod}");

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateChartData();
                    OnPropertyChanged(nameof(TotalTasks));
                    OnPropertyChanged(nameof(CompletedTasks));
                    OnPropertyChanged(nameof(PendingTasks));
                    OnPropertyChanged(nameof(CompletionRateText));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing dashboard data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateChartData()
        {
            ChartData.Clear();

            if (_currentStatistics?.ProgressData == null) return;

            var maxTasks = _currentStatistics.ProgressData.Max(p => p.TotalTasks);
            if (maxTasks == 0) maxTasks = 1; // Avoid division by zero

            foreach (var data in _currentStatistics.ProgressData)
            {
                var chartPoint = new ChartDataPoint
                {
                    Period = data.Period,
                    TotalTasks = data.TotalTasks,
                    CompletedTasks = data.CompletedTasks,
                    CompletionRate = data.CompletionRate,
                    Date = data.Date,
                    TotalBarHeight = (double)data.TotalTasks / maxTasks * 150, // Max height of 150
                    CompletedBarHeight = (double)data.CompletedTasks / maxTasks * 150
                };

                ChartData.Add(chartPoint);
            }
        }

        private void InitializeSampleData()
        {
            // Sample data for design time
            var sampleData = new[]
            {
                new ChartDataPoint { Period = "01/12", TotalTasks = 5, CompletedTasks = 4, CompletionRate = 80, TotalBarHeight = 120, CompletedBarHeight = 96 },
                new ChartDataPoint { Period = "02/12", TotalTasks = 3, CompletedTasks = 2, CompletionRate = 66.7, TotalBarHeight = 72, CompletedBarHeight = 48 },
                new ChartDataPoint { Period = "03/12", TotalTasks = 7, CompletedTasks = 7, CompletionRate = 100, TotalBarHeight = 150, CompletedBarHeight = 150 },
                new ChartDataPoint { Period = "04/12", TotalTasks = 4, CompletedTasks = 1, CompletionRate = 25, TotalBarHeight = 86, CompletedBarHeight = 21 },
                new ChartDataPoint { Period = "05/12", TotalTasks = 6, CompletedTasks = 5, CompletionRate = 83.3, TotalBarHeight = 129, CompletedBarHeight = 107 }
            };

            foreach (var item in sampleData)
            {
                ChartData.Add(item);
            }
        }

        private void OnBack()
        {
            BackRequested?.Invoke();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
