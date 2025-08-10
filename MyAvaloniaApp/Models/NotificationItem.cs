using System;

namespace MyAvaloniaApp.Models
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Duration { get; set; } = 3000; // milliseconds
        public bool IsVisible { get; set; } = true;
    }
}
