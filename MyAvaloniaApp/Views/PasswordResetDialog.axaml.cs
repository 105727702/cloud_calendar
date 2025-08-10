using Avalonia.Controls;
using MyAvaloniaApp.ViewModels;

namespace MyAvaloniaApp.Views
{
    public partial class PasswordResetDialog : Window
    {
        public PasswordResetDialog()
        {
            InitializeComponent();
        }
        
        public PasswordResetDialog(string userName, int userId) : this()
        {
            DataContext = new PasswordResetViewModel(userName, userId);
            
            // Subscribe to events
            if (DataContext is PasswordResetViewModel viewModel)
            {
                viewModel.RequestClose += (success) => 
                {
                    Close(success);
                };
            }
        }
    }
}
