using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class PasswordResetViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
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

            ResetPasswordCommand = new PasswordResetRelayCommand(async () => await ResetPasswordAsync());
            CancelCommand = new PasswordResetRelayCommand(() => RequestClose?.Invoke(false));
        }

        private async Task ResetPasswordAsync()
        {
            ErrorMessage = string.Empty;

            // Validate input
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                ErrorMessage = "Vui lòng nhập mật khẩu mới!";
                return;
            }

            if (NewPassword.Length < 6)
            {
                ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự!";
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu xác nhận không khớp!";
                return;
            }

            try
            {
                IsLoading = true;

                // Generate new salt and hash password
                var salt = GenerateSalt();
                var passwordHash = HashPassword(NewPassword, salt);

                var success = await _databaseService.ResetUserPasswordAsync(_userId, passwordHash, salt);

                if (success)
                {
                    RequestClose?.Invoke(true);
                }
                else
                {
                    ErrorMessage = "Không thể đặt lại mật khẩu. Vui lòng thử lại!";
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

        private string GenerateSalt()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var saltedPassword = password + salt;
                var bytes = Encoding.UTF8.GetBytes(saltedPassword);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
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
