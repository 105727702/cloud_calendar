using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        public static readonly BooleanToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverdue)
            {
                return isOverdue ? Brushes.Red : Brushes.Transparent;
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToWarningTextConverter : IValueConverter
    {
        public static readonly BooleanToWarningTextConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverdue)
            {
                return isOverdue ? "Quá hạn" : "";
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotificationTypeToColorConverter : IValueConverter
    {
        public static readonly NotificationTypeToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.Success => new SolidColorBrush(Color.Parse("#4CAF50")), // Green
                    NotificationType.Error => new SolidColorBrush(Color.Parse("#F44336")),   // Red
                    NotificationType.Warning => new SolidColorBrush(Color.Parse("#FF9800")), // Orange
                    NotificationType.Info => new SolidColorBrush(Color.Parse("#2196F3")),    // Blue
                    _ => new SolidColorBrush(Color.Parse("#2196F3"))
                };
            }
            return new SolidColorBrush(Color.Parse("#2196F3"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotificationTypeToIconConverter : IValueConverter
    {
        public static readonly NotificationTypeToIconConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.Success => "✓",
                    NotificationType.Error => "✕",
                    NotificationType.Warning => "⚠",
                    NotificationType.Info => "ⓘ",
                    _ => "ⓘ"
                };
            }
            return "ⓘ";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TaskWarningColorConverter : IValueConverter
    {
        public static readonly TaskWarningColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TaskItem task)
            {
                if (task.IsOverdue)
                    return Brushes.Red;
                else if (task.IsNearDeadline)
                    return Brushes.Orange;
                else
                    return Brushes.Transparent;
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TaskWarningTextConverter : IValueConverter
    {
        public static readonly TaskWarningTextConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TaskItem task)
            {
                if (task.IsOverdue)
                    return "Quá hạn";
                else if (task.IsNearDeadline)
                    return "Gần hạn";
                else
                    return "";
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToStringConverter : IValueConverter
    {
        public string TrueText { get; set; } = "True";
        public string FalseText { get; set; } = "False";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueText : FalseText;
            }
            return FalseText;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value?.ToString() == TrueText)
                return true;
            if (value?.ToString() == FalseText)
                return false;
            return false;
        }
    }
}
