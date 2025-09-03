using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class TaskRepository : BaseDatabaseService, ITaskRepository
    {
        public TaskRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<List<TaskItem>> GetAllAsync(int? userId = null)
        {
            var tasks = new List<TaskItem>();

            using var connection = CreateConnection();
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
                    Deadline = reader.GetDateTime(3),
                    Status = (TaskItemStatus)reader.GetInt32(4)
                });
            }

            return tasks;
        }

        public async Task<int> AddAsync(TaskItem task, int? userId = null)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (string.IsNullOrWhiteSpace(task.Title))
                throw new ArgumentException("Task title cannot be null or empty", nameof(task));

            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Tasks (Title, Description, Deadline, Status, UserId)
                    VALUES (@title, @description, @deadline, @status, @userId);
                    SELECT LAST_INSERT_ID();";

                command.Parameters.AddWithValue("@title", task.Title.Trim());
                command.Parameters.AddWithValue("@description", task.Description?.Trim() ?? string.Empty);
                command.Parameters.AddWithValue("@deadline", task.Deadline);
                command.Parameters.AddWithValue("@status", (int)task.Status);
                command.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);

                var result = await command.ExecuteScalarAsync();
                var taskId = Convert.ToInt32(result);
                
                LogInfo($"Task '{task.Title}' created successfully with ID: {taskId}");
                return taskId;
            }
            catch (Exception ex)
            {
                LogError($"Error creating task '{task.Title}'", ex);
                throw;
            }
        }

        public async Task UpdateAsync(TaskItem task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (string.IsNullOrWhiteSpace(task.Title))
                throw new ArgumentException("Task title cannot be null or empty", nameof(task));

            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Tasks 
                    SET Title = @title, Description = @description, Deadline = @deadline, Status = @status
                    WHERE Id = @id";

                command.Parameters.AddWithValue("@id", task.Id);
                command.Parameters.AddWithValue("@title", task.Title.Trim());
                command.Parameters.AddWithValue("@description", task.Description?.Trim() ?? string.Empty);
                command.Parameters.AddWithValue("@deadline", task.Deadline);
                command.Parameters.AddWithValue("@status", (int)task.Status);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected == 0)
                {
                    LogError($"No task found with ID: {task.Id}");
                }
                else
                {
                    LogInfo($"Task '{task.Title}' updated successfully");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error updating task with ID: {task.Id}", ex);
                throw;
            }
        }

        public async Task DeleteAsync(int taskId)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Tasks WHERE Id = @id";
            command.Parameters.AddWithValue("@id", taskId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<TaskItem>> GetNearDeadlineAsync(int? userId = null)
        {
            var tasks = new List<TaskItem>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (userId.HasValue)
            {
                command.CommandText = @"
                    SELECT Id, Title, Description, Deadline, Status 
                    FROM Tasks 
                    WHERE Status != 2 AND Deadline <= DATE_ADD(NOW(), INTERVAL 1 DAY)
                    AND (UserId = @userId OR UserId IS NULL)
                    ORDER BY Deadline";
                command.Parameters.AddWithValue("@userId", userId.Value);
            }
            else
            {
                command.CommandText = @"
                    SELECT Id, Title, Description, Deadline, Status 
                    FROM Tasks 
                    WHERE Status != 2 AND Deadline <= DATE_ADD(NOW(), INTERVAL 1 DAY)
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
                    Deadline = reader.GetDateTime(3),
                    Status = (TaskItemStatus)reader.GetInt32(4)
                });
            }

            return tasks;
        }

        public async Task ResetIdSequencePreserveDataAsync()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // MySQL - Reset AUTO_INCREMENT to start from 1
                // Bước 1: Tìm ID nhỏ nhất hiện tại
                var getMinIdCommand = connection.CreateCommand();
                getMinIdCommand.Transaction = transaction;
                getMinIdCommand.CommandText = "SELECT COALESCE(MIN(Id), 0) FROM Tasks";
                var minId = Convert.ToInt32(await getMinIdCommand.ExecuteScalarAsync());

                // Bước 2: Tạo bảng tạm để lưu dữ liệu
                var createTempCommand = connection.CreateCommand();
                createTempCommand.Transaction = transaction;
                createTempCommand.CommandText = @"
                    CREATE TEMPORARY TABLE Tasks_Temp (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        Title VARCHAR(255) NOT NULL,
                        Description TEXT,
                        Deadline DATETIME NOT NULL,
                        Status INT NOT NULL,
                        UserId INT,
                        INDEX idx_userid (UserId),
                        INDEX idx_deadline (Deadline)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
                await createTempCommand.ExecuteNonQueryAsync();

                // Bước 3: Copy dữ liệu từ bảng cũ sang bảng tạm (ID sẽ tự động được tạo lại từ 1)
                var copyDataCommand = connection.CreateCommand();
                copyDataCommand.Transaction = transaction;
                copyDataCommand.CommandText = @"
                    INSERT INTO Tasks_Temp (Title, Description, Deadline, Status, UserId)
                    SELECT Title, Description, Deadline, Status, UserId 
                    FROM Tasks 
                    ORDER BY Id";
                await copyDataCommand.ExecuteNonQueryAsync();

                // Bước 4: Xóa dữ liệu bảng cũ
                var truncateCommand = connection.CreateCommand();
                truncateCommand.Transaction = transaction;
                truncateCommand.CommandText = "TRUNCATE TABLE Tasks";
                await truncateCommand.ExecuteNonQueryAsync();

                // Bước 5: Copy dữ liệu từ bảng tạm về bảng chính
                var restoreDataCommand = connection.CreateCommand();
                restoreDataCommand.Transaction = transaction;
                restoreDataCommand.CommandText = @"
                    INSERT INTO Tasks (Title, Description, Deadline, Status, UserId)
                    SELECT Title, Description, Deadline, Status, UserId 
                    FROM Tasks_Temp 
                    ORDER BY Id";
                await restoreDataCommand.ExecuteNonQueryAsync();

                // Bước 6: Reset AUTO_INCREMENT
                var resetAutoIncrementCommand = connection.CreateCommand();
                resetAutoIncrementCommand.Transaction = transaction;
                resetAutoIncrementCommand.CommandText = "ALTER TABLE Tasks AUTO_INCREMENT = 1";
                await resetAutoIncrementCommand.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                LogInfo("Task ID sequence has been reset to start from 1. All data preserved!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                LogError("Error resetting task ID sequence", ex);
                throw;
            }
        }

        // Dashboard methods
        public async Task<List<TaskItem>> GetTasksByDateRangeAsync(DateTime startDate, DateTime endDate, int? userId = null)
        {
            var tasks = new List<TaskItem>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (userId.HasValue)
            {
                command.CommandText = @"SELECT Id, Title, Description, Deadline, Status 
                                      FROM Tasks 
                                      WHERE (UserId = @userId OR UserId IS NULL) 
                                      AND DATE(Deadline) BETWEEN DATE(@startDate) AND DATE(@endDate)
                                      ORDER BY Deadline";
                command.Parameters.AddWithValue("@userId", userId.Value);
            }
            else
            {
                command.CommandText = @"SELECT Id, Title, Description, Deadline, Status 
                                      FROM Tasks 
                                      WHERE DATE(Deadline) BETWEEN DATE(@startDate) AND DATE(@endDate)
                                      ORDER BY Deadline";
            }

            command.Parameters.AddWithValue("@startDate", startDate.Date);
            command.Parameters.AddWithValue("@endDate", endDate.Date);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tasks.Add(new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Deadline = reader.GetDateTime(3),
                    Status = (TaskItemStatus)reader.GetInt32(4)
                });
            }

            return tasks;
        }

        public async Task<Dictionary<DateTime, (int total, int completed)>> GetTaskStatsByDateAsync(DateTime startDate, DateTime endDate, int? userId = null)
        {
            var stats = new Dictionary<DateTime, (int total, int completed)>();

            using var connection = CreateConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (userId.HasValue)
            {
                command.CommandText = @"SELECT DATE(Deadline) as TaskDate, 
                                              COUNT(*) as Total,
                                              SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) as Completed
                                      FROM Tasks 
                                      WHERE (UserId = @userId OR UserId IS NULL)
                                      AND DATE(Deadline) BETWEEN DATE(@startDate) AND DATE(@endDate)
                                      GROUP BY DATE(Deadline)
                                      ORDER BY DATE(Deadline)";
                command.Parameters.AddWithValue("@userId", userId.Value);
            }
            else
            {
                command.CommandText = @"SELECT DATE(Deadline) as TaskDate, 
                                              COUNT(*) as Total,
                                              SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) as Completed
                                      FROM Tasks 
                                      WHERE DATE(Deadline) BETWEEN DATE(@startDate) AND DATE(@endDate)
                                      GROUP BY DATE(Deadline)
                                      ORDER BY DATE(Deadline)";
            }

            command.Parameters.AddWithValue("@startDate", startDate.Date);
            command.Parameters.AddWithValue("@endDate", endDate.Date);

            System.Diagnostics.Debug.WriteLine($"Dashboard SQL Query: {command.CommandText}");
            System.Diagnostics.Debug.WriteLine($"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"UserId: {userId}");

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var date = reader.GetDateTime(0).Date;
                var total = reader.GetInt32(1);
                var completed = reader.GetInt32(2);
                stats[date] = (total, completed);
                
                System.Diagnostics.Debug.WriteLine($"Found data for {date:yyyy-MM-dd}: Total={total}, Completed={completed}");
            }

            System.Diagnostics.Debug.WriteLine($"Total dates found: {stats.Count}");
            return stats;
        }
    }
}
