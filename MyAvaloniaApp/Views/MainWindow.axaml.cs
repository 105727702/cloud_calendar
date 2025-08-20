using Avalonia.Controls;
using Avalonia.Interactivity;
using MyAvaloniaApp.ViewModels;
using MyAvaloniaApp.Services;
using MyAvaloniaApp.Views;

namespace MyAvaloniaApp;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private bool _isCalendarView = true;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        
        // Register logout event
        var authService = AuthenticationService.Instance;
        authService.UserLoggedOut += OnUserLoggedOut;
        
        // Setup view toggle buttons
        SetupViewToggle();
    }

    private void SetupViewToggle()
    {
        var calendarButton = this.FindControl<Button>("CalendarViewButton");
        var listButton = this.FindControl<Button>("ListViewButton");
        var calendarView = this.FindControl<CalendarView>("CalendarViewControl");
        var listView = this.FindControl<DataGrid>("TaskListView");

        if (calendarButton != null)
        {
            calendarButton.Click += (s, e) => ToggleView(true);
        }
        
        if (listButton != null)
        {
            listButton.Click += (s, e) => ToggleView(false);
        }
    }

    private void ToggleView(bool showCalendar)
    {
        _isCalendarView = showCalendar;
        
        var calendarButton = this.FindControl<Button>("CalendarViewButton");
        var listButton = this.FindControl<Button>("ListViewButton");
        var calendarView = this.FindControl<CalendarView>("CalendarViewControl");
        var listView = this.FindControl<DataGrid>("TaskListView");

        if (calendarView != null && listView != null)
        {
            calendarView.IsVisible = showCalendar;
            listView.IsVisible = !showCalendar;
        }

        // Update button styles
        if (calendarButton != null && listButton != null)
        {
            if (showCalendar)
            {
                calendarButton.Background = Avalonia.Media.Brushes.DodgerBlue;
                listButton.Background = Avalonia.Media.Brushes.Gray;
            }
            else
            {
                calendarButton.Background = Avalonia.Media.Brushes.Gray;
                listButton.Background = Avalonia.Media.Brushes.DodgerBlue;
            }
        }
    }

    private void OnUserLoggedOut(object? sender, System.EventArgs e)
    {
        // Close MainWindow and show LoginWindow
        this.Hide();
        
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        
        loginWindow.Closed += (s, e) =>
        {
            var authService = AuthenticationService.Instance;
            if (authService.IsAuthenticated)
            {
                // Refresh data and show MainWindow again
                _viewModel?.RefreshUserInfo();
                this.Show();
            }
            else
            {
                // Exit application
                this.Close();
            }
        };
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _viewModel?.Dispose();
        var authService = AuthenticationService.Instance;
        authService.UserLoggedOut -= OnUserLoggedOut;
        base.OnClosed(e);
    }
}