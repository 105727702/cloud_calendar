using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class NotificationService
    {
        private static NotificationService? _instance;
        public static NotificationService Instance => _instance ??= new NotificationService();

        public ObservableCollection<NotificationItem> Notifications { get; } = new();

        public event Action<NotificationItem>? NotificationAdded;

        private NotificationService() { }

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int duration = 3000)
        {
            var notification = new NotificationItem
            {
                Title = title,
                Message = message,
                Type = type,
                Duration = duration,
                CreatedAt = DateTime.Now
            };

            // Thêm vào collection trên UI thread
            Dispatcher.UIThread.Post(() =>
            {
                Notifications.Insert(0, notification);
                
                // Giới hạn số lượng notifications hiển thị
                while (Notifications.Count > 5)
                {
                    Notifications.RemoveAt(Notifications.Count - 1);
                }
                
                NotificationAdded?.Invoke(notification);
            });

            // Tự động ẩn notification sau một khoảng thời gian
            if (duration > 0)
            {
                Task.Delay(duration).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (Notifications.Contains(notification))
                        {
                            notification.IsVisible = false;
                            // Xóa sau 500ms để có hiệu ứng fade out
                            Task.Delay(500).ContinueWith(__ =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    Notifications.Remove(notification);
                                });
                            });
                        }
                    });
                });
            }
        }

        public void ShowSuccess(string title, string message, int duration = 3000)
        {
            ShowNotification(title, message, NotificationType.Success, duration);
        }

        public void ShowError(string title, string message, int duration = 5000)
        {
            ShowNotification(title, message, NotificationType.Error, duration);
        }

        public void ShowWarning(string title, string message, int duration = 4000)
        {
            ShowNotification(title, message, NotificationType.Warning, duration);
        }

        public void ShowInfo(string title, string message, int duration = 3000)
        {
            ShowNotification(title, message, NotificationType.Info, duration);
        }

        public void RemoveNotification(NotificationItem notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Notifications.Remove(notification);
            });
        }

        public void ClearAllNotifications()
        {
            Dispatcher.UIThread.Post(() =>
            {
                Notifications.Clear();
            });
        }

        // Kiểm tra task sắp đến hạn và hiển thị notification
        public async Task CheckTaskDeadlinesAsync()
        {
            try
            {
                var tasksNearDeadline = await DatabaseService.Instance.GetTasksNearDeadlineAsync();
                
                foreach (var task in tasksNearDeadline)
                {
                    var hoursUntilDeadline = (task.Deadline - DateTime.Now).TotalHours;
                    
                    if (hoursUntilDeadline <= 0 && task.Status != TaskItemStatus.Completed)
                    {
                        // Task đã quá hạn
                        ShowError("Task đã quá hạn!", 
                            $"'{task.Title}' đã quá hạn {Math.Abs(hoursUntilDeadline):F1} giờ", 6000);
                    }
                    else if (hoursUntilDeadline > 0 && hoursUntilDeadline <= 24 && task.Status != TaskItemStatus.Completed)
                    {
                        // Task gần tới hạn
                        string message;
                        NotificationType type;
                        
                        if (hoursUntilDeadline <= 1)
                        {
                            var minutesLeft = Math.Max(1, (int)(hoursUntilDeadline * 60));
                            message = $"'{task.Title}' sẽ đến hạn trong {minutesLeft} phút!";
                            type = NotificationType.Error;
                        }
                        else if (hoursUntilDeadline <= 3)
                        {
                            message = $"'{task.Title}' sẽ đến hạn trong {hoursUntilDeadline:F1} giờ!";
                            type = NotificationType.Error;
                        }
                        else if (hoursUntilDeadline <= 6)
                        {
                            message = $"'{task.Title}' sẽ đến hạn trong {hoursUntilDeadline:F1} giờ";
                            type = NotificationType.Warning;
                        }
                        else
                        {
                            message = $"'{task.Title}' sẽ đến hạn trong {hoursUntilDeadline:F1} giờ";
                            type = NotificationType.Info;
                        }
                        
                        ShowNotification("Nhắc nhở deadline", message, type, 5000);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Lỗi", $"Không thể kiểm tra deadline: {ex.Message}");
            }
        }
    }
}
