using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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

        private async Task CreateUsersTableAsync(SqliteConnection connection)
        {
            // Tạo bảng Users
            var userCommand = connection.CreateCommand();
            userCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    Email TEXT,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    LastLoginAt TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Role INTEGER NOT NULL DEFAULT 0
                )";
            userCommand.ExecuteNonQuery();

            // Thêm cột Role nếu chưa tồn tại (cho compatibility với database cũ)
            await AddColumnIfNotExistsAsync(connection, "Users", "Role", "INTEGER NOT NULL DEFAULT 0");
            
            // Thêm cột Email nếu chưa tồn tại (cho compatibility với database cũ)
            await AddColumnIfNotExistsAsync(connection, "Users", "Email", "TEXT");
        }

        private async Task CreateTasksTableAsync(SqliteConnection connection)
        {
            // Tạo bảng Tasks (cập nhật để liên kết với UserId)
            var taskCommand = connection.CreateCommand();
            taskCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Deadline TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    UserId INTEGER,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                )";
            taskCommand.ExecuteNonQuery();

            // Thêm cột UserId nếu chưa tồn tại (cho compatibility với database cũ)
            await AddColumnIfNotExistsAsync(connection, "Tasks", "UserId", "INTEGER");
        }

        private Task AddColumnIfNotExistsAsync(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                alterCommand.ExecuteNonQuery();
            }
            catch
            {
                // Cột đã tồn tại, bỏ qua
            }
            return Task.CompletedTask;
        }

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
