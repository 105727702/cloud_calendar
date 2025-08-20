using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyAvaloniaApp.Services;
using MyAvaloniaApp.Views;

namespace MyAvaloniaApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Initialize database when application starts
            var _ = DatabaseService.Instance;
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var authService = AuthenticationService.Instance;
                
                // Thử khôi phục session từ JWT token
                bool sessionRestored = await authService.TryRestoreSessionAsync();
                
                if (sessionRestored)
                {
                    // Đã có session hợp lệ, mở MainWindow trực tiếp
                    var mainWindow = new MainWindow();
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                else
                {
                    // Không có session, hiển thị login screen
                    var loginWindow = new LoginWindow();
                    desktop.MainWindow = loginWindow;
                    
                    // Wait for successful login event
                    loginWindow.Closed += (sender, e) =>
                    {
                        if (authService.IsAuthenticated)
                        {
                            // Login successful, show MainWindow
                            var mainWindow = new MainWindow();
                            mainWindow.Show();
                            desktop.MainWindow = mainWindow;
                        }
                        else
                        {
                            // User cancelled login, exit application
                            desktop.Shutdown();
                        }
                    };
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"App initialization error: {ex.Message}");
            throw;
        }
    }
}