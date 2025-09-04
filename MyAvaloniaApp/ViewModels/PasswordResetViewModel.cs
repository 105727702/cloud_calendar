using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class PasswordResetViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly PasswordManager _passwordManager;
        private readonly int _userId;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<bool>? RequestClose;

        public string UserName { get; }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand ResetPasswordCommand { get; }
        public ICommand CancelCommand { get; }

        public PasswordResetViewModel(string userName, int userId)
        {
            UserName = userName;
            _userId = userId;
            _databaseService = DatabaseService.Instance;
            _passwordManager = new PasswordManager();
            
            ResetPasswordCommand = new PasswordResetRelayCommand(async () => await ResetPasswordAsync());
            CancelCommand = new PasswordResetRelayCommand(() => RequestClose?.Invoke(false));
        }        private async Task ResetPasswordAsync()
        {
            ErrorMessage = string.Empty;

            // Validate input
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                ErrorMessage = "Please enter a new password!";
                return;
            }

            if (NewPassword.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters!";
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Password confirmation does not match!";
                return;
            }

            try
            {
                IsLoading = true;

                // Sử dụng PasswordManager thay vì method riêng
                var success = await _databaseService.ResetUserPasswordAsync(_userId, NewPassword);

                if (success)
                {
                    RequestClose?.Invoke(true);
                }
                else
                {
                    ErrorMessage = "Unable to reset password. Please try again!";
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

    public class PasswordResetRelayCommand : ICommand
    {
        private readonly Action? _execute;
        private readonly Func<Task>? _executeAsync;
        private readonly Func<bool>? _canExecute;

        public PasswordResetRelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public PasswordResetRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
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
