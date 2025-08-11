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
        private static readonly object _lock = new object();
        
        // Repository instances
        private readonly ITaskRepository _taskRepository;
        private readonly IUserRepository _userRepository;
        private readonly PasswordManager _passwordManager;
        private readonly DatabaseInitializer _initializer;
        
        public static DatabaseService Instance 
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new DatabaseService();
                    }
                }
                return _instance;
            }
        }

        private DatabaseService()
        {
            try
            {
                // Tạo file database trong thư mục gốc của dự án để dễ dàng truy cập bằng SQLite viewer
                var currentDirectory = Directory.GetCurrentDirectory();
                var dbPath = Path.Combine(currentDirectory, "tasks.db");
                _connectionString = $"Data Source={dbPath}";
                Console.WriteLine($"Database will be created at: {dbPath}");
                
                // Initialize repositories and services
                _passwordManager = new PasswordManager();
                _taskRepository = new TaskRepository(_connectionString);
                _userRepository = new UserRepository(_connectionString, _passwordManager);
                _initializer = new DatabaseInitializer(_connectionString, _passwordManager);
                
                InitializeDatabaseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization error: {ex.Message}");
                throw new InvalidOperationException("Failed to initialize database service", ex);
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            await _initializer.InitializeAsync();
        }

        // Task management methods - delegating to TaskRepository
        public async Task<List<TaskItem>> GetAllTasksAsync(int? userId = null)
        {
            return await _taskRepository.GetAllAsync(userId);
        }

        public async Task<int> AddTaskAsync(TaskItem task, int? userId = null)
        {
            return await _taskRepository.AddAsync(task, userId);
        }

        public async Task UpdateTaskAsync(TaskItem task)
        {
            await _taskRepository.UpdateAsync(task);
        }

        public async Task DeleteTaskAsync(int taskId)
        {
            await _taskRepository.DeleteAsync(taskId);
        }

        public async Task<List<TaskItem>> GetTasksNearDeadlineAsync(int? userId = null)
        {
            return await _taskRepository.GetNearDeadlineAsync(userId);
        }

        // User management methods - delegating to UserRepository
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _userRepository.GetByUsernameAsync(username);
        }

        public async Task<int> CreateUserAsync(User user)
        {
            return await _userRepository.CreateAsync(user);
        }

        public async Task UpdateUserLastLoginAsync(int userId, DateTime lastLoginAt)
        {
            await _userRepository.UpdateLastLoginAsync(userId, lastLoginAt);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _userRepository.GetAllAsync();
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            return await _userRepository.DeleteAsync(userId);
        }

        public async Task<bool> ResetUserPasswordAsync(int userId, string newPasswordHash, string newSalt)
        {
            // For backward compatibility with existing signature
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

        // New method with better signature
        public async Task<bool> ResetUserPasswordAsync(int userId, string newPassword)
        {
            return await _userRepository.ResetPasswordAsync(userId, newPassword);
        }

        // Password validation methods
        public bool ValidateUserPassword(User user, string password)
        {
            return _userRepository.ValidatePassword(user, password);
        }

        public string GenerateSalt()
        {
            return _passwordManager.GenerateSalt();
        }

        public string HashPassword(string password, string salt)
        {
            return _passwordManager.HashPassword(password, salt);
        }

        // Task ID sequence management - delegating to TaskRepository
        public async Task ResetTaskIdSequencePreserveDataAsync()
        {
            await _taskRepository.ResetIdSequencePreserveDataAsync();
        }
    }
}
