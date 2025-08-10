using Avalonia.Controls;
using MyAvaloniaApp.ViewModels;
using MyAvaloniaApp.Services;
using MyAvaloniaApp.Views;

namespace MyAvaloniaApp;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        
        // Đăng ký sự kiện logout
        var authService = AuthenticationService.Instance;
        authService.UserLoggedOut += OnUserLoggedOut;
    }

    private void OnUserLoggedOut(object? sender, System.EventArgs e)
    {
        // Đóng MainWindow và hiển thị LoginWindow
        this.Hide();
        
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        
        loginWindow.Closed += (s, e) =>
        {
            var authService = AuthenticationService.Instance;
            if (authService.IsAuthenticated)
            {
                // Refresh data và hiển thị lại MainWindow
                _viewModel?.RefreshUserInfo();
                this.Show();
            }
            else
            {
                // Thoát ứng dụng
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