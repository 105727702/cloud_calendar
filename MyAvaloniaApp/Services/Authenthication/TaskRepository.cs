using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
                    Deadline = DateTime.Parse(reader.GetString(3)),
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
                    SELECT last_insert_rowid();";

                command.Parameters.AddWithValue("@title", task.Title.Trim());
                command.Parameters.AddWithValue("@description", task.Description?.Trim() ?? string.Empty);
                command.Parameters.AddWithValue("@deadline", task.Deadline.ToString("yyyy-MM-dd HH:mm:ss"));
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
                command.Parameters.AddWithValue("@deadline", task.Deadline.ToString("yyyy-MM-dd HH:mm:ss"));
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

        public async Task ResetIdSequencePreserveDataAsync()
        {
            using var connection = CreateConnection();
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

            command.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

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

            command.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            System.Diagnostics.Debug.WriteLine($"Dashboard SQL Query: {command.CommandText}");
            System.Diagnostics.Debug.WriteLine($"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"UserId: {userId}");

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var date = DateTime.Parse(reader.GetString(0));
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
