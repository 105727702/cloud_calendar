using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class UserRepository : BaseDatabaseService, IUserRepository
    {
        private readonly PasswordManager _passwordManager;

        public UserRepository(string connectionString, PasswordManager passwordManager) 
            : base(connectionString)
        {
            _passwordManager = passwordManager;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                LogError("Username cannot be null or empty");
                return null;
            }

            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Username, PasswordHash, Salt, CreatedAt, LastLoginAt, IsActive, Role 
                    FROM Users 
                    WHERE Username = @username COLLATE NOCASE";
                command.Parameters.AddWithValue("@username", username.Trim());

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
            catch (Exception ex)
            {
                LogError("Error getting user by username", ex);
                throw;
            }
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Username, PasswordHash, Salt, CreatedAt, LastLoginAt, IsActive, Role 
                    FROM Users 
                    WHERE Id = @id";
                command.Parameters.AddWithValue("@id", userId);

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
            catch (Exception ex)
            {
                LogError("Error getting user by id", ex);
                throw;
            }
        }

        public async Task<int> CreateAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(user.Username))
                throw new ArgumentException("Username cannot be null or empty", nameof(user));

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                throw new ArgumentException("Password hash cannot be null or empty", nameof(user));

            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Users (Username, PasswordHash, Salt, CreatedAt, LastLoginAt, IsActive, Role)
                    VALUES (@username, @passwordHash, @salt, @createdAt, @lastLoginAt, @isActive, @role);
                    SELECT last_insert_rowid();";

                command.Parameters.AddWithValue("@username", user.Username.Trim());
                command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
                command.Parameters.AddWithValue("@salt", user.Salt);
                command.Parameters.AddWithValue("@createdAt", user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@lastLoginAt", user.LastLoginAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@role", (int)user.Role);

                var result = await command.ExecuteScalarAsync();
                var userId = Convert.ToInt32(result);
                
                LogInfo($"User '{user.Username}' created successfully with ID: {userId}");
                return userId;
            }
            catch (Exception ex)
            {
                LogError($"Error creating user '{user.Username}'", ex);
                throw;
            }
        }

        public async Task UpdateLastLoginAsync(int userId, DateTime lastLoginAt)
        {
            using var connection = CreateConnection();
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

        public async Task<List<User>> GetAllAsync()
        {
            var users = new List<User>();
            using var connection = CreateConnection();
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

        public async Task<bool> UpdateAsync(User user)
        {
            using var connection = CreateConnection();
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

        public async Task<bool> DeleteAsync(int userId)
        {
            using var connection = CreateConnection();
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
                LogError("Error deleting user", ex);
                throw;
            }
        }

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            var salt = _passwordManager.GenerateSalt();
            var passwordHash = _passwordManager.HashPassword(newPassword, salt);

            using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users 
                SET PasswordHash = @passwordHash, Salt = @salt
                WHERE Id = @id";

            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@passwordHash", passwordHash);
            command.Parameters.AddWithValue("@salt", salt);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public bool ValidatePassword(User user, string password)
        {
            return _passwordManager.VerifyPassword(password, user.PasswordHash, user.Salt);
        }
    }
}
