using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MyAvaloniaApp.ViewModels;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Views
{
    public partial class CalendarView : UserControl
    {
        private MainWindowViewModel? _viewModel;
        private readonly Dictionary<DayOfWeek, Grid> _dayColumns = new();
        private const int HoursShown = 18; // Show 6 AM to 11 PM
        private const int StartHour = 6;
        private const double HourHeight = 60.0;

        public CalendarView()
        {
            InitializeComponent();
            InitializeCalendar();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Ensure calendar is properly initialized when the control is loaded
            RefreshTasks();
        }

        private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Clean up event subscriptions to prevent memory leaks
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.FilteredTasks.CollectionChanged -= FilteredTasks_CollectionChanged;
            }
        }

        private void InitializeCalendar()
        {
            CreateTimeLabels();
            SetupDayColumns();
            CreateHourLines();
        }

        private void CreateTimeLabels()
        {
            var timeLabels = this.FindControl<StackPanel>("TimeLabels");
            if (timeLabels == null) return;

            timeLabels.Children.Clear();

            for (int hour = StartHour; hour <= StartHour + HoursShown; hour++)
            {
                var displayHour = hour > 24 ? hour - 24 : hour;
                var timeText = $"{displayHour:D2}:00";
                if (displayHour == 0) timeText = "00:00";

                var border = new Border
                {
                    Height = HourHeight,
                    BorderBrush = new SolidColorBrush(Color.Parse("#dee2e6")),
                    BorderThickness = new Thickness(0, 1, 1, 0),
                    Padding = new Thickness(5),
                    Child = new TextBlock
                    {
                        Text = timeText,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse("#6c757d")),
                        VerticalAlignment = VerticalAlignment.Top
                    }
                };

                timeLabels.Children.Add(border);
            }
        }

        private void SetupDayColumns()
        {
            var dayNames = new[] { "SundayColumn", "MondayColumn", "TuesdayColumn", 
                                  "WednesdayColumn", "ThursdayColumn", "FridayColumn", "SaturdayColumn" };
            var dayOfWeeks = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, 
                                    DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

            for (int i = 0; i < dayNames.Length; i++)
            {
                var column = this.FindControl<Grid>(dayNames[i]);
                if (column != null)
                {
                    _dayColumns[dayOfWeeks[i]] = column;
                    column.Height = (HoursShown + 1) * HourHeight;
                }
            }
        }

        private void CreateHourLines()
        {
            foreach (var column in _dayColumns.Values)
            {
                for (int hour = 0; hour <= HoursShown; hour++)
                {
                    var line = new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.Parse("#dee2e6")),
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, hour * HourHeight, 0, 0)
                    };
                    column.Children.Add(line);
                }

                // Add right border
                var rightBorder = new Border
                {
                    Width = 1,
                    Background = new SolidColorBrush(Color.Parse("#dee2e6")),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Height = (HoursShown + 1) * HourHeight
                };
                column.Children.Add(rightBorder);
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.FilteredTasks.CollectionChanged -= FilteredTasks_CollectionChanged;
            }

            _viewModel = DataContext as MainWindowViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.FilteredTasks.CollectionChanged += FilteredTasks_CollectionChanged;
                RefreshTasks();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.FilteredTasks) ||
                e.PropertyName == "CurrentWeekStart")
            {
                RefreshTasks();
            }
        }

        private void FilteredTasks_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Refresh calendar whenever FilteredTasks collection changes
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshTasks();
            });
        }

        private void RefreshTasks()
        {
            if (_viewModel?.FilteredTasks == null) return;

            // Clear existing task blocks
            foreach (var column in _dayColumns.Values)
            {
                var tasksToRemove = column.Children.OfType<Border>()
                    .Where(b => b.Classes.Contains("task-block"))
                    .ToList();

                foreach (var task in tasksToRemove)
                {
                    column.Children.Remove(task);
                }
            }

            // Add current week tasks  
            var weekStart = _viewModel.GetCurrentWeekStart();
            var weekEnd = weekStart.AddDays(7);

            var weekTasks = _viewModel.FilteredTasks
                .Where(t => t.Deadline >= weekStart && t.Deadline < weekEnd)
                .ToList();

            foreach (var task in weekTasks)
            {
                AddTaskToCalendar(task);
            }
        }

        private void AddTaskToCalendar(TaskItem task)
        {
            var dayOfWeek = task.Deadline.DayOfWeek;
            if (!_dayColumns.TryGetValue(dayOfWeek, out var column)) return;

            var hour = task.Deadline.Hour;
            var minute = task.Deadline.Minute;

            // Only show tasks within our time range
            if (hour < StartHour || hour > StartHour + HoursShown) return;

            var topPosition = (hour - StartHour) * HourHeight + (minute / 60.0 * HourHeight);

            var taskBlock = new Border
            {
                Background = GetTaskStatusBrush(task.Status),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4),
                Margin = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Height = Math.Max(30, HourHeight * 0.6), // Minimum height
                Classes = { "task-block" },
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = task.Title,
                            FontSize = 11,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = GetTaskTextBrush(task.Status),
                            TextWrapping = TextWrapping.Wrap,
                            MaxLines = 2
                        },
                        new TextBlock
                        {
                            Text = task.Deadline.ToString("HH:mm"),
                            FontSize = 9,
                            Foreground = GetTaskTextBrush(task.Status),
                            Opacity = 0.9
                        }
                    }
                }
            };

            taskBlock.Margin = new Thickness(2, topPosition, 4, 0);
            
            // Handle click event để có thể chỉnh sửa task
            taskBlock.PointerPressed += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.SelectedTask = task;
                }
            };

            column.Children.Add(taskBlock);
        }

        private IBrush GetTaskStatusBrush(TaskItemStatus status)
        {
            return status switch
            {
                TaskItemStatus.NotStarted => new SolidColorBrush(Color.Parse("#e74c3c")), // Bright Red
                TaskItemStatus.InProgress => new SolidColorBrush(Color.Parse("#f39c12")), // Orange (instead of yellow)
                TaskItemStatus.Completed => new SolidColorBrush(Color.Parse("#27ae60")), // Green
                _ => new SolidColorBrush(Color.Parse("#95a5a6")) // Light Gray
            };
        }

        private IBrush GetTaskTextBrush(TaskItemStatus status)
        {
            return status switch
            {
                TaskItemStatus.NotStarted => Brushes.White, // White text on red background
                TaskItemStatus.InProgress => Brushes.White, // White text on orange background
                TaskItemStatus.Completed => Brushes.White, // White text on green background
                _ => Brushes.White // White text on gray background
            };
        }
    }
}
