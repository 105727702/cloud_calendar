using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private static DatabaseService? _instance;
        
        public static DatabaseService Instance => _instance ??= new DatabaseService();

        public DatabaseService()
        {
            try
            {
                // Tạo file database trong thư mục gốc của dự án để dễ dàng truy cập bằng SQLite viewer
                var currentDirectory = Directory.GetCurrentDirectory();
                var dbPath = Path.Combine(currentDirectory, "tasks.db");
                _connectionString = $"Data Source={dbPath}";
                Console.WriteLine($"Database will be created at: {dbPath}");
                InitializeDatabase();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization error: {ex.Message}");
                throw;
            }
        }

        private async void InitializeDatabase()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Tạo bảng Users
                var userCommand = connection.CreateCommand();
                userCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        Salt TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        LastLoginAt TEXT NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        Role INTEGER NOT NULL DEFAULT 0
                    )";
                userCommand.ExecuteNonQuery();

                // Thêm cột Role nếu chưa tồn tại (cho compatibility với database cũ)
                try
                {
                    var alterRoleCommand = connection.CreateCommand();
                    alterRoleCommand.CommandText = "ALTER TABLE Users ADD COLUMN Role INTEGER NOT NULL DEFAULT 0";
                    alterRoleCommand.ExecuteNonQuery();
                }
                catch
                {
                    // Cột đã tồn tại, bỏ qua
                }

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
                try
                {
                    var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Tasks ADD COLUMN UserId INTEGER";
                    alterCommand.ExecuteNonQuery();
                }
                catch
                {
                    // Cột đã tồn tại, bỏ qua
                }
                
                Console.WriteLine("Database initialized successfully");
                
                // Tự động tạo tài khoản admin nếu chưa tồn tại
                await EnsureAdminExistsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database table creation error: {ex.Message}");
                throw;
            }
        }

        private async Task EnsureAdminExistsAsync()
        {
            try
            {
                var adminUser = await GetUserByUsernameAsync("admin");
                if (adminUser == null)
                {
                    // Tạo tài khoản admin mặc định
                    var salt = GenerateSalt();
                    var passwordHash = HashPassword("123456", salt);

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

                    await CreateUserAsync(admin);
                    Console.WriteLine("Default admin account created: admin/123456");
                }
                else if (adminUser.Role != UserRole.Admin)
                {
                    // Đảm bảo tài khoản admin có quyền Admin
                    adminUser.Role = UserRole.Admin;
                    await UpdateUserAsync(adminUser);
                    Console.WriteLine("Admin account role updated");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring admin exists: {ex.Message}");
            }
        }

        private string GenerateSalt()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var saltBytes = new byte[32];
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        private string HashPassword(string password, string salt)
        {
            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                password, 
                Convert.FromBase64String(salt), 
                100000, 
                System.Security.Cryptography.HashAlgorithmName.SHA256);
            return Convert.ToBase64String(pbkdf2.GetBytes(32));
        }

        public async Task<List<TaskItem>> GetAllTasksAsync(int? userId = null)
        {
            var tasks = new List<TaskItem>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (userId.HasValue)
            {
                command.CommandText = "SELECT Id, Title, Description, Deadline, Status FROM Tasks WHERE UserId = @userId OR UserId IS NULL ORDER BY Deadline";
                command.Parameters.AddWithValue("@userId", userId.Value);
            }
            else
            {
                command.CommandText = "SELECT Id, Title, Description, Deadline, Status FROM Tasks ORDER BY Deadline";
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tasks.Add(new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Deadline = DateTime.Parse(reader.GetString(3)),
                    Status = (TaskItemStatus)reader.GetInt32(4)
                });
            }

            return tasks;
        }

        public async Task<int> AddTaskAsync(TaskItem task, int? userId = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Tasks (Title, Description, Deadline, Status, UserId)
                VALUES (@title, @description, @deadline, @status, @userId);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@title", task.Title);
            command.Parameters.AddWithValue("@description", task.Description);
            command.Parameters.AddWithValue("@deadline", task.Deadline.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@status", (int)task.Status);
            command.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateTaskAsync(TaskItem task)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tasks 
                SET Title = @title, Description = @description, Deadline = @deadline, Status = @status
                WHERE Id = @id";

            command.Parameters.AddWithValue("@id", task.Id);
            command.Parameters.AddWithValue("@title", task.Title);
            command.Parameters.AddWithValue("@description", task.Description);
            command.Parameters.AddWithValue("@deadline", task.Deadline.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@status", (int)task.Status);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteTaskAsync(int taskId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Tasks WHERE Id = @id";
            command.Parameters.AddWithValue("@id", taskId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<TaskItem>> GetTasksNearDeadlineAsync(int? userId = null)
        {
            var tasks = new List<TaskItem>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (userId.HasValue)
            {
                command.CommandText = @"
                    SELECT Id, Title, Description, Deadline, Status 
                    FROM Tasks 
                    WHERE Status != 2 AND datetime(Deadline) <= datetime('now', '+1 day')
                    AND (UserId = @userId OR UserId IS NULL)
                    ORDER BY Deadline";
                command.Parameters.AddWithValue("@userId", userId.Value);
            }
            else
            {
                command.CommandText = @"
                    SELECT Id, Title, Description, Deadline, Status 
                    FROM Tasks 
                    WHERE Status != 2 AND datetime(Deadline) <= datetime('now', '+1 day')
                    ORDER BY Deadline";
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tasks.Add(new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Deadline = DateTime.Parse(reader.GetString(3)),
                    Status = (TaskItemStatus)reader.GetInt32(4)
                });
            }

            return tasks;
        }

        // User management methods
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Username, PasswordHash, Salt, CreatedAt, LastLoginAt, IsActive, Role 
                FROM Users 
                WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Salt = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    LastLoginAt = DateTime.Parse(reader.GetString(5)),
                    IsActive = reader.GetInt32(6) == 1,
                    Role = (UserRole)reader.GetInt32(7)
                };
            }

            return null;
        }

        public async Task<int> CreateUserAsync(User user)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Users (Username, PasswordHash, Salt, CreatedAt, LastLoginAt, IsActive, Role)
                VALUES (@username, @passwordHash, @salt, @createdAt, @lastLoginAt, @isActive, @role);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            command.Parameters.AddWithValue("@salt", user.Salt);
            command.Parameters.AddWithValue("@createdAt", user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@lastLoginAt", user.LastLoginAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@role", (int)user.Role);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateUserLastLoginAsync(int userId, DateTime lastLoginAt)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users 
                SET LastLoginAt = @lastLoginAt
                WHERE Id = @id";

            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@lastLoginAt", lastLoginAt.ToString("yyyy-MM-dd HH:mm:ss"));

            await command.ExecuteNonQueryAsync();
        }

        // Admin functions for user management
        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = new List<User>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Username, PasswordHash, Salt, CreatedAt, LastLoginAt, IsActive, Role 
                FROM Users 
                ORDER BY CreatedAt DESC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Salt = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    LastLoginAt = DateTime.Parse(reader.GetString(5)),
                    IsActive = reader.GetInt32(6) == 1,
                    Role = (UserRole)reader.GetInt32(7)
                });
            }

            return users;
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users 
                SET Username = @username, IsActive = @isActive, Role = @role
                WHERE Id = @id";

            command.Parameters.AddWithValue("@id", user.Id);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@role", (int)user.Role);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Sử dụng transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = connection.BeginTransaction();

            try
            {
                // Xóa tất cả tasks của user trước
                var deleteTasksCommand = connection.CreateCommand();
                deleteTasksCommand.Transaction = transaction;
                deleteTasksCommand.CommandText = "DELETE FROM Tasks WHERE UserId = @userId";
                deleteTasksCommand.Parameters.AddWithValue("@userId", userId);
                await deleteTasksCommand.ExecuteNonQueryAsync();

                // Sau đó xóa user
                var deleteUserCommand = connection.CreateCommand();
                deleteUserCommand.Transaction = transaction;
                deleteUserCommand.CommandText = "DELETE FROM Users WHERE Id = @id";
                deleteUserCommand.Parameters.AddWithValue("@id", userId);

                var rowsAffected = await deleteUserCommand.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    await transaction.CommitAsync();
                    return true;
                }
                else
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error deleting user: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ResetUserPasswordAsync(int userId, string newPasswordHash, string newSalt)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users 
                SET PasswordHash = @passwordHash, Salt = @salt
                WHERE Id = @id";

            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@passwordHash", newPasswordHash);
            command.Parameters.AddWithValue("@salt", newSalt);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        // Method để reset task ID sequence về 1 mà KHÔNG mất dữ liệu
        public async Task ResetTaskIdSequencePreserveDataAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Bước 1: Tạo bảng tạm với cấu trúc giống Tasks
                var createTempCommand = connection.CreateCommand();
                createTempCommand.Transaction = transaction;
                createTempCommand.CommandText = @"
                    CREATE TABLE Tasks_Temp (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Description TEXT,
                        Deadline TEXT NOT NULL,
                        Status INTEGER NOT NULL,
                        UserId INTEGER,
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                    )";
                await createTempCommand.ExecuteNonQueryAsync();

                // Bước 2: Copy dữ liệu từ bảng cũ sang bảng mới (ID sẽ tự động được tạo lại từ 1)
                var copyDataCommand = connection.CreateCommand();
                copyDataCommand.Transaction = transaction;
                copyDataCommand.CommandText = @"
                    INSERT INTO Tasks_Temp (Title, Description, Deadline, Status, UserId)
                    SELECT Title, Description, Deadline, Status, UserId 
                    FROM Tasks 
                    ORDER BY Id";
                await copyDataCommand.ExecuteNonQueryAsync();

                // Bước 3: Xóa bảng cũ
                var dropOldCommand = connection.CreateCommand();
                dropOldCommand.Transaction = transaction;
                dropOldCommand.CommandText = "DROP TABLE Tasks";
                await dropOldCommand.ExecuteNonQueryAsync();

                // Bước 4: Đổi tên bảng mới thành Tasks
                var renameCommand = connection.CreateCommand();
                renameCommand.Transaction = transaction;
                renameCommand.CommandText = "ALTER TABLE Tasks_Temp RENAME TO Tasks";
                await renameCommand.ExecuteNonQueryAsync();

                // Bước 5: Reset sqlite_sequence để đảm bảo ID tiếp theo bắt đầu đúng
                var resetSequenceCommand = connection.CreateCommand();
                resetSequenceCommand.Transaction = transaction;
                resetSequenceCommand.CommandText = "DELETE FROM sqlite_sequence WHERE name='Tasks'";
                await resetSequenceCommand.ExecuteNonQueryAsync();

                // Bước 6: Cập nhật sqlite_sequence với số lượng record hiện tại
                var updateSequenceCommand = connection.CreateCommand();
                updateSequenceCommand.Transaction = transaction;
                updateSequenceCommand.CommandText = @"
                    INSERT INTO sqlite_sequence (name, seq) 
                    SELECT 'Tasks', COUNT(*) FROM Tasks";
                await updateSequenceCommand.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                Console.WriteLine("Task ID sequence has been reset to start from 1. All data preserved!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error resetting task ID sequence: {ex.Message}");
                throw;
            }
        }
    }
}
