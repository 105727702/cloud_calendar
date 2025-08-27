using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MyAvaloniaApp.Services;
using MyAvaloniaApp.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyAvaloniaApp.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly AuthenticationService _authService;
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private bool _isLoading = false;
        private bool _isLoginMode = true;
        private bool _isUsernameLogin = true;
        private Window? _parentWindow;

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

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
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
                    OnPropertyChanged(nameof(UsernameLabel));
                    OnPropertyChanged(nameof(UsernamePlaceholder));
                    ErrorMessage = string.Empty;
                    SuccessMessage = string.Empty;
                }
            }
        }

        public bool IsUsernameLogin
        {
            get => _isUsernameLogin;
            set
            {
                if (SetProperty(ref _isUsernameLogin, value))
                {
                    OnPropertyChanged(nameof(IsEmailLogin));
                    OnPropertyChanged(nameof(UsernameLabel));
                    OnPropertyChanged(nameof(UsernamePlaceholder));
                    Username = string.Empty; // Clear input when switching
                    ErrorMessage = string.Empty;
                }
            }
        }

        public bool IsEmailLogin
        {
            get => !_isUsernameLogin;
            set => IsUsernameLogin = !value;
        }

        public string UsernameLabel => IsLoginMode 
            ? (IsUsernameLogin ? "Username:" : "Email:") 
            : "Username:";
        
        public string UsernamePlaceholder => IsLoginMode 
            ? (IsUsernameLogin ? "Enter username" : "Enter email address") 
            : "Enter username";

        public string ModeText => IsLoginMode ? "Login" : "Register";
        public string SwitchModeText => IsLoginMode ? "Don't have an account? Register" : "Already have an account? Login";

        public ICommand LoginCommand { get; }
        public ICommand SwitchModeCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand CancelCommand { get; }

        public LoginViewModel()
        {
            _authService = AuthenticationService.Instance;
            
            LoginCommand = new LoginRelayCommand(async () => await ExecuteLoginAsync());
            SwitchModeCommand = new LoginRelayCommand(() => IsLoginMode = !IsLoginMode);
            ForgotPasswordCommand = new LoginRelayCommand(async () => await ExecuteForgotPasswordAsync());
            CancelCommand = new LoginRelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
        }

        public void SetParentWindow(Window parentWindow)
        {
            _parentWindow = parentWindow;
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
                    // Determine login method based on user selection
                    if (IsUsernameLogin)
                    {
                        success = await _authService.LoginAsync(Username, Password);
                    }
                    else
                    {
                        // Login with email
                        success = await _authService.LoginByEmailAsync(Username, Password);
                    }
                    
                    if (!success)
                    {
                        ErrorMessage = IsUsernameLogin 
                            ? "Incorrect username or password!" 
                            : "Incorrect email or password!";
                    }
                    else
                    {
                        // Show login success notification
                        NotificationService.Instance.ShowSuccess(
                            "Login Successful",
                            $"Welcome {Username}!"
                        );
                        
                        // Login successful - close login window
                        LoginSuccessful?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    if (Password.Length < 6)
                    {
                        ErrorMessage = "Password must be at least 6 characters!";
                        return;
                    }

                    success = await _authService.RegisterAsync(Username, Password, Email);
                    if (!success)
                    {
                        ErrorMessage = "Username or email already exists!";
                    }
                    else
                    {
                        // Show registration success notification in LoginWindow
                        ErrorMessage = string.Empty; // Clear any error first
                        
                        // Show success message in UI
                        await Task.Delay(100); // Small delay to ensure UI updates
                        
                        // Show success message
                        SuccessMessage = "✅ Registration successful! Account has been created. Switching to login mode...";
                        
                        // Wait 2.5 seconds for user to read message
                        await Task.Delay(2500);
                        
                        // Reset message and switch to login mode
                        SuccessMessage = string.Empty;
                        IsLoginMode = true;
                        
                        // Reset password for user to re-enter
                        Password = string.Empty;
                        Email = string.Empty;
                        
                        // Show notification after mode switch
                        NotificationService.Instance.ShowSuccess(
                            "Registration Successful",
                            "Account has been created successfully. You can now log in."
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExecuteForgotPasswordAsync()
        {
            if (IsLoading) return;

            try
            {
                // Tạo dialog để người dùng nhập username
                var forgotPasswordDialog = new ForgotPasswordDialog();
                
                // Sử dụng parent window đã được set
                if (_parentWindow == null) return;
                
                var result = await forgotPasswordDialog.ShowDialog<string?>(_parentWindow);

                if (!string.IsNullOrEmpty(result))
                {
                    // Kiểm tra xem user có tồn tại không
                    var user = await DatabaseService.Instance.GetUserByUsernameAsync(result);
                    if (user != null)
                    {
                        // Mở dialog reset password
                        var resetDialog = new PasswordResetDialog();
                        var resetViewModel = new PasswordResetViewModel(user.Username, user.Id);
                        resetDialog.DataContext = resetViewModel;

                        // Subscribe to close event
                        resetViewModel.RequestClose += (success) =>
                        {
                            if (success)
                            {
                                NotificationService.Instance.ShowSuccess(
                                    "Password Reset Successful",
                                    "Your password has been reset successfully. You can now log in with your new password."
                                );
                                // Clear current form
                                Username = string.Empty;
                                Password = string.Empty;
                                ErrorMessage = string.Empty;
                                SuccessMessage = string.Empty;
                            }
                            resetDialog.Close();
                        };

                        await resetDialog.ShowDialog(_parentWindow);
                    }
                    else
                    {
                        ErrorMessage = "Username not found!";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
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
