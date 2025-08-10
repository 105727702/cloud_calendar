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
            // Tạo file database trong thư mục gốc của dự án để dễ dàng truy cập bằng SQLite viewer
            var currentDirectory = Directory.GetCurrentDirectory();
            var dbPath = Path.Combine(currentDirectory, "tasks.db");
            _connectionString = $"Data Source={dbPath}";
            Console.WriteLine($"Database will be created at: {dbPath}");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Deadline TEXT NOT NULL,
                    Status INTEGER NOT NULL
                )";
            command.ExecuteNonQuery();
        }

        public async Task<List<TaskItem>> GetAllTasksAsync()
        {
            var tasks = new List<TaskItem>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, Deadline, Status FROM Tasks ORDER BY Deadline";

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

        public async Task<int> AddTaskAsync(TaskItem task)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Tasks (Title, Description, Deadline, Status)
                VALUES (@title, @description, @deadline, @status);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@title", task.Title);
            command.Parameters.AddWithValue("@description", task.Description);
            command.Parameters.AddWithValue("@deadline", task.Deadline.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@status", (int)task.Status);

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

        public async Task<List<TaskItem>> GetTasksNearDeadlineAsync()
        {
            var tasks = new List<TaskItem>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Title, Description, Deadline, Status 
                FROM Tasks 
                WHERE Status != 2 AND datetime(Deadline) <= datetime('now', '+1 day')
                ORDER BY Deadline";

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
    }
}
