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

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Initialize database when application starts
            var _ = DatabaseService.Instance;
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Show login screen first
                var loginWindow = new LoginWindow();
                desktop.MainWindow = loginWindow;
                
                // Wait for successful login event
                loginWindow.Closed += (sender, e) =>
                {
                    var authService = AuthenticationService.Instance;
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

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"App initialization error: {ex.Message}");
            throw;
        }
    }
}