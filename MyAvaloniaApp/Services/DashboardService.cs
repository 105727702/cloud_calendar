using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class DashboardService
    {
        private readonly TaskRepository _taskRepository;

        public DashboardService(TaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<DashboardStatistics> GetDashboardStatisticsAsync(TimePeriod timePeriod, int? userId = null)
        {
            var (startDate, endDate) = GetDateRange(timePeriod);
            
            System.Diagnostics.Debug.WriteLine($"Getting dashboard stats for period {timePeriod}, user {userId}");
            System.Diagnostics.Debug.WriteLine($"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            
            // Debug: Get all tasks to see what's in the database
            var allTasks = await _taskRepository.GetAllAsync(userId);
            System.Diagnostics.Debug.WriteLine($"Total tasks in database for user {userId}: {allTasks.Count}");
            foreach (var task in allTasks)
            {
                System.Diagnostics.Debug.WriteLine($"Task: {task.Title}, Deadline: {task.Deadline:yyyy-MM-dd}, Status: {task.Status}");
            }
            
            var taskStats = await _taskRepository.GetTaskStatsByDateAsync(startDate, endDate, userId);
            
            var statistics = new DashboardStatistics
            {
                TimePeriod = timePeriod,
                ProgressData = new List<TaskProgressData>()
            };

            switch (timePeriod)
            {
                case TimePeriod.Day:
                    statistics.ProgressData = GenerateDayStatistics(taskStats, startDate, endDate);
                    break;
                case TimePeriod.Week:
                    statistics.ProgressData = GenerateWeekStatistics(taskStats, startDate, endDate);
                    break;
                case TimePeriod.Month:
                    statistics.ProgressData = GenerateMonthStatistics(taskStats, startDate, endDate);
                    break;
            }

            // Calculate overall statistics
            statistics.TotalTasksInPeriod = statistics.ProgressData.Sum(p => p.TotalTasks);
            statistics.CompletedTasksInPeriod = statistics.ProgressData.Sum(p => p.CompletedTasks);

            return statistics;
        }

        private (DateTime startDate, DateTime endDate) GetDateRange(TimePeriod timePeriod)
        {
            var now = DateTime.Now;
            
            return timePeriod switch
            {
                TimePeriod.Day => (now.AddDays(-15), now.AddDays(15)), // 15 days before and after today
                TimePeriod.Week => (GetStartOfWeek(now.AddDays(-42)), now.AddDays(21)), // 6 weeks before, 3 weeks after
                TimePeriod.Month => (new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1)), // This whole year
                _ => (now.AddDays(-15), now.AddDays(15))
            };
        }

        private List<TaskProgressData> GenerateDayStatistics(Dictionary<DateTime, (int total, int completed)> taskStats, DateTime startDate, DateTime endDate)
        {
            var result = new List<TaskProgressData>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                var (total, completed) = taskStats.GetValueOrDefault(currentDate, (0, 0));
                
                result.Add(new TaskProgressData
                {
                    Period = currentDate.ToString("dd/MM"),
                    Date = currentDate,
                    TotalTasks = total,
                    CompletedTasks = completed
                });

                currentDate = currentDate.AddDays(1);
            }

            return result;
        }

        private List<TaskProgressData> GenerateWeekStatistics(Dictionary<DateTime, (int total, int completed)> taskStats, DateTime startDate, DateTime endDate)
        {
            var result = new List<TaskProgressData>();
            var currentWeekStart = GetStartOfWeek(startDate);

            while (currentWeekStart <= endDate)
            {
                var weekEnd = currentWeekStart.AddDays(6);
                var weekStats = taskStats
                    .Where(kvp => kvp.Key >= currentWeekStart && kvp.Key <= weekEnd)
                    .Select(kvp => kvp.Value);

                var totalTasks = weekStats.Sum(s => s.total);
                var completedTasks = weekStats.Sum(s => s.completed);

                result.Add(new TaskProgressData
                {
                    Period = $"Tuần {GetWeekOfYear(currentWeekStart)}",
                    Date = currentWeekStart,
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks
                });

                currentWeekStart = currentWeekStart.AddDays(7);
            }

            return result;
        }

        private List<TaskProgressData> GenerateMonthStatistics(Dictionary<DateTime, (int total, int completed)> taskStats, DateTime startDate, DateTime endDate)
        {
            var result = new List<TaskProgressData>();
            var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);

            while (currentMonth <= endDate)
            {
                var monthEnd = currentMonth.AddMonths(1).AddDays(-1);
                var monthStats = taskStats
                    .Where(kvp => kvp.Key >= currentMonth && kvp.Key <= monthEnd)
                    .Select(kvp => kvp.Value);

                var totalTasks = monthStats.Sum(s => s.total);
                var completedTasks = monthStats.Sum(s => s.completed);

                result.Add(new TaskProgressData
                {
                    Period = currentMonth.ToString("MM/yyyy"),
                    Date = currentMonth,
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks
                });

                currentMonth = currentMonth.AddMonths(1);
            }

            return result;
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            var dayOfWeek = (int)date.DayOfWeek;
            var monday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek; // Convert Sunday=0 to Monday=1
            return date.AddDays(monday).Date;
        }

        private int GetWeekOfYear(DateTime date)
        {
            var culture = CultureInfo.CurrentCulture;
            var calendar = culture.Calendar;
            return calendar.GetWeekOfYear(date, culture.DateTimeFormat.CalendarWeekRule, DayOfWeek.Monday);
        }
    }
}
