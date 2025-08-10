using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly AuthenticationService _authService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private bool _isLoading = false;
        private bool _isLoginMode = true;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? LoginSuccessful;
        public event EventHandler? RequestClose;

        public string Username
        {
            get => _username;
            set 
            { 
                if (SetProperty(ref _username, value))
                {
                    ((LoginRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set 
            { 
                if (SetProperty(ref _password, value))
                {
                    ((LoginRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string SuccessMessage
        {
            get => _successMessage;
            set => SetProperty(ref _successMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                if (SetProperty(ref _isLoading, value))
                {
                    ((LoginRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoginMode
        {
            get => _isLoginMode;
            set
            {
                if (SetProperty(ref _isLoginMode, value))
                {
                    OnPropertyChanged(nameof(ModeText));
                    OnPropertyChanged(nameof(SwitchModeText));
                    ErrorMessage = string.Empty;
                    SuccessMessage = string.Empty;
                }
            }
        }

        public string ModeText => IsLoginMode ? "Đăng nhập" : "Đăng ký";
        public string SwitchModeText => IsLoginMode ? "Chưa có tài khoản? Đăng ký" : "Đã có tài khoản? Đăng nhập";

        public ICommand LoginCommand { get; }
        public ICommand SwitchModeCommand { get; }
        public ICommand CancelCommand { get; }

        public LoginViewModel()
        {
            _authService = AuthenticationService.Instance;
            
            LoginCommand = new LoginRelayCommand(async () => await ExecuteLoginAsync());
            SwitchModeCommand = new LoginRelayCommand(() => IsLoginMode = !IsLoginMode);
            CancelCommand = new LoginRelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
        }

        private bool CanExecuteLogin()
        {
            return !IsLoading && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        private async Task ExecuteLoginAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            try
            {
                bool success;
                
                if (IsLoginMode)
                {
                    success = await _authService.LoginAsync(Username, Password);
                    if (!success)
                    {
                        ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng!";
                    }
                    else
                    {
                        // Hiển thị thông báo đăng nhập thành công
                        NotificationService.Instance.ShowSuccess(
                            "Đăng nhập thành công",
                            $"Chào mừng {Username}!"
                        );
                        
                        // Đăng nhập thành công - đóng cửa sổ login
                        LoginSuccessful?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    if (Password.Length < 6)
                    {
                        ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự!";
                        return;
                    }

                    success = await _authService.RegisterAsync(Username, Password);
                    if (!success)
                    {
                        ErrorMessage = "Tên đăng nhập đã tồn tại!";
                    }
                    else
                    {
                        // Hiển thị thông báo đăng ký thành công trong LoginWindow
                        ErrorMessage = string.Empty; // Clear any error first
                        
                        // Hiển thị thông báo thành công trong UI
                        await Task.Delay(100); // Small delay to ensure UI updates
                        
                        // Hiển thị success message
                        SuccessMessage = "✅ Đăng ký thành công! Tài khoản đã được tạo. Chuyển về chế độ đăng nhập...";
                        
                        // Đợi 2.5 giây để user đọc message
                        await Task.Delay(2500);
                        
                        // Reset message và chuyển về chế độ đăng nhập
                        SuccessMessage = string.Empty;
                        IsLoginMode = true;
                        
                        // Reset password để user nhập lại
                        Password = string.Empty;
                        
                        // Hiển thị notification sau khi đã chuyển mode
                        NotificationService.Instance.ShowSuccess(
                            "Đăng ký thành công",
                            "Tài khoản đã được tạo thành công. Bạn có thể đăng nhập ngay bây giờ."
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class LoginRelayCommand : ICommand
    {
        private readonly Action? _execute;
        private readonly Func<Task>? _executeAsync;
        private readonly Func<bool>? _canExecute;

        public LoginRelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public LoginRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
