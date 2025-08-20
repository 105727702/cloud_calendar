using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace MyAvaloniaApp.Views
{
    public partial class ForgotPasswordDialog : Window
    {
        public ForgotPasswordDialog()
        {
            InitializeComponent();
        }

        private void ContinueButton_Click(object? sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text?.Trim();
            
            if (string.IsNullOrEmpty(username))
            {
                ErrorTextBlock.Text = "Please enter your username!";
                ErrorTextBlock.IsVisible = true;
                return;
            }

            ErrorTextBlock.IsVisible = false;
            Close(username);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
