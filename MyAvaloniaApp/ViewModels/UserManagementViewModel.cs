using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MyAvaloniaApp.Models;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.ViewModels
{
    public class UserManagementViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly AuthenticationService _authService;
        private readonly Views.AdminNotificationPanel? _notificationPanel;
        private Window? _ownerWindow;
        private bool _isLoading = false;
        private User? _selectedUser;

        public ObservableCollection<User> Users { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? RequestClose;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public User? SelectedUser
        {
            get => _selectedUser;
            set 
            { 
                if (SetProperty(ref _selectedUser, value))
                {
                    // Cập nhật trạng thái của các command khi SelectedUser thay đổi
                    (EditUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ResetPasswordCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ToggleUserStatusCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand CreateDefaultAdminCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand ToggleUserStatusCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand CloseCommand { get; }

        public UserManagementViewModel(Views.AdminNotificationPanel? notificationPanel = null, Window? ownerWindow = null)
        {
            _databaseService = DatabaseService.Instance;
            _authService = AuthenticationService.Instance;
            _notificationPanel = notificationPanel;
            _ownerWindow = ownerWindow;

            RefreshCommand = new RelayCommand(async () => await LoadUsersAsync());
            CreateDefaultAdminCommand = new RelayCommand(async () => await CreateDefaultAdminAsync());
            EditUserCommand = new RelayCommand(async () => await EditUserAsync(), () => SelectedUser != null);
            ResetPasswordCommand = new RelayCommand(async () => await ResetPasswordAsync(), () => SelectedUser != null);
            ToggleUserStatusCommand = new RelayCommand(async () => await ToggleUserStatusAsync(), () => SelectedUser != null);
            DeleteUserCommand = new RelayCommand(async () => await DeleteUserAsync(), () => SelectedUser != null);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

            // Load initial data
            Task.Run(async () => await LoadUsersAsync());
        }

        private async Task LoadUsersAsync()
        {
            IsLoading = true;
            try
            {
                var users = await _databaseService.GetAllUsersAsync();
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Users.Clear();
                    foreach (var user in users)
                    {
                        Users.Add(user);
                    }
                });
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync("Lỗi", $"Không thể tải danh sách người dùng: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateDefaultAdminAsync()
        {
            try
            {
                // Check if admin already exists
                if (Users.Any(u => u.Role == UserRole.Admin))
                {
                    await ShowWarningMessageAsync("Thông báo", "Đã tồn tại tài khoản Admin trong hệ thống.");
                    return;
                }

                IsLoading = true;

                // Create default admin user with password 123456
                var salt = GenerateSalt();
                var passwordHash = HashPassword("123456", salt);

                var adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = passwordHash,
                    Salt = salt,
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    LastLoginAt = DateTime.Now
                };

                await _databaseService.CreateUserAsync(adminUser);
                await LoadUsersAsync();

                await ShowSuccessMessageAsync(
                    "Tạo Admin thành công",
                    "Tài khoản admin mặc định đã được tạo.\nTên đăng nhập: admin\nMật khẩu: 123456"
                );
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync("Lỗi", $"Không thể tạo tài khoản admin: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task EditUserAsync()
        {
            if (SelectedUser == null) return;

            var selectedUserName = SelectedUser.Username; // Lưu tên user trước khi thực hiện thao tác

            try
            {
                // For now, just toggle role between User and Admin
                var newRole = SelectedUser.Role == UserRole.Admin ? UserRole.User : UserRole.Admin;
                
                var updatedUser = new User
                {
                    Id = SelectedUser.Id,
                    Username = SelectedUser.Username,
                    Role = newRole,
                    IsActive = SelectedUser.IsActive,
                    PasswordHash = SelectedUser.PasswordHash,
                    Salt = SelectedUser.Salt,
                    CreatedAt = SelectedUser.CreatedAt,
                    LastLoginAt = SelectedUser.LastLoginAt
                };

                IsLoading = true;
                var success = await _databaseService.UpdateUserAsync(updatedUser);
                
                if (success)
                {
                    await LoadUsersAsync();
                    await ShowSuccessMessageAsync("Thành công", $"Đã cập nhật quyền cho người dùng {selectedUserName}");
                }
                else
                {
                    await ShowErrorMessageAsync("Lỗi", "Không thể cập nhật người dùng. Vui lòng thử lại.");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync("Lỗi", $"Không thể cập nhật người dùng: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetPasswordAsync()
        {
            if (SelectedUser == null) return;

            try
            {
                // Mở dialog để nhập mật khẩu mới
                var passwordResetDialog = new Views.PasswordResetDialog(SelectedUser.Username, SelectedUser.Id);
                
                // Sử dụng owner window được truyền vào, hoặc fallback về MainWindow
                var owner = _ownerWindow ?? 
                    (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                        ? desktop.MainWindow 
                        : null);
                
                bool? result = null;
                if (owner != null)
                {
                    result = await passwordResetDialog.ShowDialog<bool?>(owner);
                }
                else
                {
                    passwordResetDialog.Show();
                    return; // Không thể xử lý kết quả nếu không có owner
                }

                if (result == true)
                {
                    // Reload danh sách users để cập nhật thông tin
                    await LoadUsersAsync();
                    
                    await ShowSuccessMessageAsync(
                        "Đặt lại mật khẩu thành công",
                        $"Mật khẩu cho người dùng {SelectedUser?.Username} đã được thay đổi."
                    );
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync("Lỗi", $"Không thể mở dialog đặt lại mật khẩu: {ex.Message}");
            }
        }

        private async Task ToggleUserStatusAsync()
        {
            if (SelectedUser == null) return;

            var selectedUserName = SelectedUser.Username; // Lưu tên user trước khi thực hiện thao tác
            var currentStatus = SelectedUser.IsActive;

            try
            {
                IsLoading = true;

                var updatedUser = new User
                {
                    Id = SelectedUser.Id,
                    Username = SelectedUser.Username,
                    Role = SelectedUser.Role,
                    IsActive = !SelectedUser.IsActive,
                    PasswordHash = SelectedUser.PasswordHash,
                    Salt = SelectedUser.Salt,
                    CreatedAt = SelectedUser.CreatedAt,
                    LastLoginAt = SelectedUser.LastLoginAt
                };

                var success = await _databaseService.UpdateUserAsync(updatedUser);
                
                if (success)
                {
                    await LoadUsersAsync();
                    var status = !currentStatus ? "mở khóa" : "khóa";
                    await ShowSuccessMessageAsync("Thành công", $"Đã {status} người dùng {selectedUserName}");
                }
                else
                {
                    await ShowErrorMessageAsync("Lỗi", "Không thể thay đổi trạng thái người dùng. Vui lòng thử lại.");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync("Lỗi", $"Không thể thay đổi trạng thái người dùng: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;

            var selectedUserName = SelectedUser.Username; // Lưu tên user trước khi thực hiện thao tác

            // Prevent deleting current user or last admin
            if (SelectedUser.Id == _authService.CurrentUser?.Id)
            {
                await ShowWarningMessageAsync("Cảnh báo", "Không thể xóa tài khoản đang đăng nhập!");
                return;
            }

            if (SelectedUser.Role == UserRole.Admin && Users.Count(u => u.Role == UserRole.Admin) <= 1)
            {
                await ShowWarningMessageAsync("Cảnh báo", "Không thể xóa tài khoản Admin cuối cùng!");
                return;
            }

            try
            {
                IsLoading = true;

                var success = await _databaseService.DeleteUserAsync(SelectedUser.Id);
                
                if (success)
                {
                    await LoadUsersAsync();
                    await ShowSuccessMessageAsync("Thành công", $"Đã xóa người dùng {selectedUserName}");
                    SelectedUser = null;
                }
                else
                {
                    await ShowErrorMessageAsync("Lỗi", "Không thể xóa người dùng. Vui lòng thử lại.");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync("Lỗi", $"Không thể xóa người dùng: {ex.Message}");
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

        // Helper methods for showing messages
        private async Task ShowSuccessMessageAsync(string title, string message)
        {
            if (_notificationPanel != null)
            {
                await _notificationPanel.ShowSuccessAsync(title, message);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Console.WriteLine($"SUCCESS: {title} - {message}");
                });
            }
        }

        private async Task ShowErrorMessageAsync(string title, string message)
        {
            if (_notificationPanel != null)
            {
                await _notificationPanel.ShowErrorAsync(title, message);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Console.WriteLine($"ERROR: {title} - {message}");
                });
            }
        }

        private async Task ShowWarningMessageAsync(string title, string message)
        {
            if (_notificationPanel != null)
            {
                await _notificationPanel.ShowWarningAsync(title, message);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Console.WriteLine($"WARNING: {title} - {message}");
                });
            }
        }

        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action? _execute;
        private readonly Func<Task>? _executeAsync;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
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

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
