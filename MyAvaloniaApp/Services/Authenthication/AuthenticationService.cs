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
        private readonly JwtService _jwtService;
        private readonly UserCacheManager _cacheManager;
        private readonly RateLimiter _rateLimiter;
        private readonly RequestQueueManager _queueManager;
        private readonly PerformanceMonitor _performanceMonitor;
        
        public static AuthenticationService Instance => _instance ??= new AuthenticationService();
        
        public User? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;
        public bool IsAdmin => CurrentUser?.IsAdmin ?? false;
        
        public event EventHandler<User>? UserLoggedIn;
        public event EventHandler? UserLoggedOut;

        private AuthenticationService()
        {
            _databaseService = DatabaseService.Instance;
            _jwtService = JwtService.Instance;
            _cacheManager = UserCacheManager.Instance;
            _rateLimiter = RateLimiter.Instance;
            _queueManager = RequestQueueManager.Instance;
            _performanceMonitor = PerformanceMonitor.Instance;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            using var measurement = _performanceMonitor.StartMeasurement("Login");
            
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    measurement.MarkFailure();
                    return false;
                }

                // Rate limiting check
                var clientId = $"login_{username.ToLower()}";
                if (!await _rateLimiter.CanMakeRequestAsync(clientId))
                {
                    measurement.MarkFailure();
                    var retryAfter = _rateLimiter.GetRetryAfter(clientId);
                    throw new InvalidOperationException($"Too many login attempts. Please try again in {retryAfter.TotalSeconds:F0} seconds.");
                }

                // Queue the request để tránh overload database
                var result = await _queueManager.EnqueueRequestAsync(async () =>
                {
                    using var dbMeasurement = _performanceMonitor.StartMeasurement("DatabaseLogin");
                    
                    try
                    {
                        // Kiểm tra cache trước
                        var cachedUser = _cacheManager.GetCachedUserByUsername(username);
                        if (cachedUser != null && VerifyPassword(password, cachedUser.PasswordHash, cachedUser.Salt))
                        {
                            if (!cachedUser.IsActive)
                            {
                                dbMeasurement.MarkFailure();
                                return false;
                            }
                            
                            CurrentUser = cachedUser;
                            cachedUser.LastLoginAt = DateTime.Now;
                            await _databaseService.UpdateUserLastLoginAsync(cachedUser.Id, cachedUser.LastLoginAt);
                            
                            // Tạo và lưu JWT token
                            var token = _jwtService.GenerateToken(cachedUser);
                            _jwtService.SaveToken(token);
                            
                            UserLoggedIn?.Invoke(this, cachedUser);
                            return true;
                        }

                        // Nếu không có trong cache, query database
                        var user = await _databaseService.GetUserByUsernameAsync(username);
                        if (user == null || !user.IsActive)
                        {
                            dbMeasurement.MarkFailure();
                            return false;
                        }

                        if (VerifyPassword(password, user.PasswordHash, user.Salt))
                        {
                            CurrentUser = user;
                            user.LastLoginAt = DateTime.Now;
                            await _databaseService.UpdateUserLastLoginAsync(user.Id, user.LastLoginAt);
                            
                            // Cache user để lần sau nhanh hơn
                            _cacheManager.CacheUser(user);
                            
                            // Tạo và lưu JWT token
                            var token = _jwtService.GenerateToken(user);
                            _jwtService.SaveToken(token);
                            
                            UserLoggedIn?.Invoke(this, user);
                            return true;
                        }
                        
                        dbMeasurement.MarkFailure();
                        return false;
                    }
                    catch
                    {
                        dbMeasurement.MarkFailure();
                        return false;
                    }
                });

                if (!result)
                {
                    measurement.MarkFailure();
                }

                return result;
            }
            catch
            {
                measurement.MarkFailure();
                throw;
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
                    
                    // Tạo và lưu JWT token
                    var token = _jwtService.GenerateToken(user);
                    _jwtService.SaveToken(token);
                    
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
            if (CurrentUser != null)
            {
                _cacheManager.InvalidateUser(CurrentUser.Id); // Xóa user khỏi cache
            }
            CurrentUser = null;
            _jwtService.DeleteToken(); // Xóa JWT token khi logout
            UserLoggedOut?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> TryRestoreSessionAsync()
        {
            try
            {
                var token = _jwtService.LoadToken();
                if (string.IsNullOrEmpty(token))
                    return false;

                var user = _jwtService.GetUserFromToken(token);
                if (user == null)
                {
                    _jwtService.DeleteToken(); // Token không hợp lệ, xóa nó
                    return false;
                }

                // Kiểm tra cache trước
                var cachedUser = _cacheManager.GetCachedUserById(user.Id);
                if (cachedUser != null && cachedUser.IsActive)
                {
                    CurrentUser = cachedUser;
                    return true;
                }

                // Kiểm tra user vẫn còn active trong database
                var dbUser = await _databaseService.GetUserByIdAsync(user.Id);
                if (dbUser == null || !dbUser.IsActive)
                {
                    _jwtService.DeleteToken(); // User không còn active, xóa token
                    _cacheManager.InvalidateUser(user.Id); // Xóa khỏi cache
                    return false;
                }

                // Cache user và khôi phục session
                _cacheManager.CacheUser(dbUser);
                CurrentUser = dbUser;
                return true;
            }
            catch
            {
                _jwtService.DeleteToken(); // Có lỗi xảy ra, xóa token
                return false;
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

        private bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }
    }
}
