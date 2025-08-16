using System;
using System.Collections.Generic;

namespace MyAvaloniaApp.Models
{
    public enum TimePeriod
    {
        Day,
        Week,
        Month
    }

    public class TaskProgressData
    {
        public string Period { get; set; } = string.Empty;
        public int CompletedTasks { get; set; }
        public int TotalTasks { get; set; }
        public double CompletionRate => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks * 100 : 0;
        public DateTime Date { get; set; }
    }

    public class DashboardStatistics
    {
        public List<TaskProgressData> ProgressData { get; set; } = new();
        public TimePeriod TimePeriod { get; set; }
        public int TotalTasksInPeriod { get; set; }
        public int CompletedTasksInPeriod { get; set; }
        public double OverallCompletionRate => TotalTasksInPeriod > 0 ? (double)CompletedTasksInPeriod / TotalTasksInPeriod * 100 : 0;
    }
}
