using Avalonia.Controls;
using Avalonia.Media;
using MyAvaloniaApp.ViewModels;
using System.ComponentModel;

namespace MyAvaloniaApp.Views
{
    public partial class LoginWindow : Window
    {
        private LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            DataContext = _viewModel;
            
            _viewModel.LoginSuccessful += OnLoginSuccessful;
            _viewModel.RequestClose += OnRequestClose;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginViewModel.IsLoginMode))
            {
                // Trigger animation bằng cách thay đổi RenderTransform
                AnimateModeSwitch();
            }
        }

        private void AnimateModeSwitch()
        {
            // Tạo hiệu ứng slide nhẹ khi chuyển đổi chế độ
            var formContainer = this.FindControl<StackPanel>("FormContainer");
            var buttonContainer = this.FindControl<StackPanel>("ButtonContainer");
            var modeTextBlock = this.FindControl<TextBlock>("ModeTextBlock");

            if (formContainer != null)
            {
                // Tạo hiệu ứng slide từ phải sang trái
                formContainer.RenderTransform = new TranslateTransform(20, 0);
                formContainer.Opacity = 0.7;
                
                // Reset về vị trí ban đầu (animation sẽ tự động chạy nhờ Transitions)
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    formContainer.RenderTransform = new TranslateTransform(0, 0);
                    formContainer.Opacity = 1.0;
                });
            }

            if (buttonContainer != null)
            {
                buttonContainer.RenderTransform = new TranslateTransform(-20, 0);
                buttonContainer.Opacity = 0.7;
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    buttonContainer.RenderTransform = new TranslateTransform(0, 0);
                    buttonContainer.Opacity = 1.0;
                });
            }

            if (modeTextBlock != null)
            {
                modeTextBlock.Opacity = 0.5;
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    modeTextBlock.Opacity = 1.0;
                });
            }
        }

        private void OnLoginSuccessful(object? sender, System.EventArgs e)
        {
            Close(true);
        }

        private void OnRequestClose(object? sender, System.EventArgs e)
        {
            Close(false);
        }
    }
}
