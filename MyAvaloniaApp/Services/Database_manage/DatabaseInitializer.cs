using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class DatabaseInitializer : BaseDatabaseService
    {
        private readonly PasswordManager _passwordManager;

        public DatabaseInitializer(string connectionString, PasswordManager passwordManager) 
            : base(connectionString)
        {
            _passwordManager = passwordManager;
        }

        public async Task InitializeAsync()
        {
            try
            {
                using var connection = CreateConnection();
                connection.Open();

                // Tạo database nếu chưa tồn tại
                await CreateDatabaseIfNotExistsAsync();
                
                await CreateUsersTableAsync(connection);
                await CreateTasksTableAsync(connection);

                LogInfo("Database initialized successfully");
                
                await EnsureAdminExistsAsync();
            }
            catch (Exception ex)
            {
                LogError("Database table creation error", ex);
                throw;
            }
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            // Tạo connection đến MySQL server (không chỉ định database)
            var serverConnectionString = $"Server={DatabaseConfiguration.MySQL.Server};Port={DatabaseConfiguration.MySQL.Port};Uid={DatabaseConfiguration.MySQL.Username};Pwd={DatabaseConfiguration.MySQL.Password};";
            
            using var connection = new MySqlConnection(serverConnectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{DatabaseConfiguration.MySQL.Database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            await command.ExecuteNonQueryAsync();
            
            LogInfo($"Database '{DatabaseConfiguration.MySQL.Database}' created or already exists");
        }

        private async Task CreateUsersTableAsync(MySqlConnection connection)
        {
            // Tạo bảng Users với MySQL syntax
            var userCommand = connection.CreateCommand();
            userCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Username VARCHAR(255) NOT NULL UNIQUE,
                    Email VARCHAR(255),
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    LastLoginAt DATETIME NOT NULL,
                    IsActive TINYINT(1) NOT NULL DEFAULT 1,
                    Role INT NOT NULL DEFAULT 0,
                    INDEX idx_username (Username),
                    INDEX idx_email (Email)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
            await userCommand.ExecuteNonQueryAsync();

            // MySQL không cần thêm cột như SQLite, ta đã định nghĩa đầy đủ từ đầu
            LogInfo("Users table created successfully");
        }

        private async Task CreateTasksTableAsync(MySqlConnection connection)
        {
            // Tạo bảng Tasks với MySQL syntax
            var taskCommand = connection.CreateCommand();
            taskCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Tasks (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Title VARCHAR(255) NOT NULL,
                    Description TEXT,
                    Deadline DATETIME NOT NULL,
                    Status INT NOT NULL,
                    UserId INT,
                    INDEX idx_userid (UserId),
                    INDEX idx_deadline (Deadline),
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
            await taskCommand.ExecuteNonQueryAsync();

            LogInfo("Tasks table created successfully");
        }

        // MySQL không cần method AddColumnIfNotExistsAsync như SQLite
        // Vì ta đã định nghĩa đầy đủ schema từ đầu

        private async Task EnsureAdminExistsAsync()
        {
            try
            {
                var userRepository = new UserRepository(_connectionString, _passwordManager);
                var adminUser = await userRepository.GetByUsernameAsync("admin");
                
                if (adminUser == null)
                {
                    // Tạo tài khoản admin mặc định
                    var salt = _passwordManager.GenerateSalt();
                    var passwordHash = _passwordManager.HashPassword("123456", salt);

                    Console.WriteLine($"=== ADMIN CREDENTIALS ===");
                    Console.WriteLine($"Username: admin");
                    Console.WriteLine($"Password: 123456");
                    Console.WriteLine($"Salt: {salt}");
                    Console.WriteLine($"Hash: {passwordHash}");
                    Console.WriteLine($"========================");

                    var admin = new User
                    {
                        Username = "admin",
                        PasswordHash = passwordHash,
                        Salt = salt,
                        Role = UserRole.Admin,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        LastLoginAt = DateTime.Now
                    };

                    await userRepository.CreateAsync(admin);
                    LogInfo("Default admin account created: admin/123456");
                }
                else if (adminUser.Role != UserRole.Admin)
                {
                    // Đảm bảo tài khoản admin có quyền Admin
                    adminUser.Role = UserRole.Admin;
                    await userRepository.UpdateAsync(adminUser);
                    LogInfo("Admin account role updated");
                }
            }
            catch (Exception ex)
            {
                LogError("Error ensuring admin exists", ex);
            }
        }
    }
}
