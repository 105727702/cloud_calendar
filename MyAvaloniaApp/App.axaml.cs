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
            // Khởi tạo database khi ứng dụng bắt đầu
            var _ = DatabaseService.Instance;
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Hiển thị màn hình đăng nhập trước
                var loginWindow = new LoginWindow();
                desktop.MainWindow = loginWindow;
                
                // Đợi sự kiện đăng nhập thành công
                loginWindow.Closed += (sender, e) =>
                {
                    var authService = AuthenticationService.Instance;
                    if (authService.IsAuthenticated)
                    {
                        // Đăng nhập thành công, hiển thị MainWindow
                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        desktop.MainWindow = mainWindow;
                    }
                    else
                    {
                        // Người dùng hủy đăng nhập, thoát ứng dụng
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