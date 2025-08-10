using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class AuthenticationService
    {
        private static AuthenticationService? _instance;
        private readonly DatabaseService _databaseService;
        
        public static AuthenticationService Instance => _instance ??= new AuthenticationService();
        
        public User? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;
        public bool IsAdmin => CurrentUser?.IsAdmin ?? false;
        
        public event EventHandler<User>? UserLoggedIn;
        public event EventHandler? UserLoggedOut;

        private AuthenticationService()
        {
            _databaseService = DatabaseService.Instance;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var user = await _databaseService.GetUserByUsernameAsync(username);
                if (user == null || !user.IsActive)
                {
                    return false;
                }

                if (VerifyPassword(password, user.PasswordHash, user.Salt))
                {
                    CurrentUser = user;
                    user.LastLoginAt = DateTime.Now;
                    await _databaseService.UpdateUserLastLoginAsync(user.Id, user.LastLoginAt);
                    UserLoggedIn?.Invoke(this, user);
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RegisterAsync(string username, string password)
        {
            try
            {
                // Kiểm tra xem username đã tồn tại chưa
                var existingUser = await _databaseService.GetUserByUsernameAsync(username);
                if (existingUser != null)
                {
                    return false;
                }

                // Tạo salt và hash password
                var salt = GenerateSalt();
                var passwordHash = HashPassword(password, salt);

                var user = new User
                {
                    Username = username,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    CreatedAt = DateTime.Now,
                    LastLoginAt = DateTime.Now,
                    IsActive = true,
                    // Chỉ tài khoản "admin" mới được quyền Admin, các tài khoản khác đều là User
                    Role = username.ToLower() == "admin" ? UserRole.Admin : UserRole.User
                };

                var userId = await _databaseService.CreateUserAsync(user);
                if (userId > 0)
                {
                    user.Id = userId;
                    CurrentUser = user;
                    UserLoggedIn?.Invoke(this, user);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Logout()
        {
            CurrentUser = null;
            UserLoggedOut?.Invoke(this, EventArgs.Empty);
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

        private bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }
    }
}
