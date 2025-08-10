using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace MyAvaloniaApp.Views
{
    public partial class AdminNotificationPanel : UserControl
    {
        private Border? _notificationBorder;
        private TextBlock? _notificationIcon;
        private TextBlock? _notificationTitle;
        private TextBlock? _notificationMessage;

        public AdminNotificationPanel()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeControls()
        {
            _notificationBorder = this.FindControl<Border>("NotificationBorder");
            _notificationIcon = this.FindControl<TextBlock>("NotificationIcon");
            _notificationTitle = this.FindControl<TextBlock>("NotificationTitle");
            _notificationMessage = this.FindControl<TextBlock>("NotificationMessage");
        }

        public async Task ShowSuccessAsync(string title, string message)
        {
            await ShowNotificationAsync(title, message, "✓", "#4CAF50"); // Green
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            await ShowNotificationAsync(title, message, "✗", "#F44336"); // Red
        }

        public async Task ShowWarningAsync(string title, string message)
        {
            await ShowNotificationAsync(title, message, "⚠", "#FF9800"); // Orange
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            await ShowNotificationAsync(title, message, "ℹ", "#2196F3"); // Blue
        }

        private async Task ShowNotificationAsync(string title, string message, string icon, string backgroundColor)
        {
            if (_notificationBorder == null || _notificationIcon == null || 
                _notificationTitle == null || _notificationMessage == null)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _notificationIcon.Text = icon;
                _notificationTitle.Text = title;
                _notificationMessage.Text = message;
                _notificationBorder.Background = new SolidColorBrush(Color.Parse(backgroundColor));
                _notificationBorder.IsVisible = true;
            });

            // Tự động ẩn sau 4 giây (tăng thời gian để đọc thông báo tạo admin)
            await Task.Delay(4000);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _notificationBorder.IsVisible = false;
            });
        }

        public void Hide()
        {
            if (_notificationBorder != null)
            {
                _notificationBorder.IsVisible = false;
            }
        }
    }
}
