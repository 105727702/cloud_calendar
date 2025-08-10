using Avalonia.Controls;
using MyAvaloniaApp.ViewModels;

namespace MyAvaloniaApp.Views
{
    public partial class UserManagementWindow : Window
    {
        public UserManagementWindow()
        {
            InitializeComponent();
            
            // Tìm notification panel
            var notificationPanel = this.FindControl<AdminNotificationPanel>("NotificationPanel");
            
            DataContext = new UserManagementViewModel(notificationPanel, this);
            
            // Subscribe to close event
            if (DataContext is UserManagementViewModel viewModel)
            {
                viewModel.RequestClose += (_, _) => Close();
            }
        }
    }
}
