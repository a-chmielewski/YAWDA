using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace YAWDA.Converters
{
    /// <summary>
    /// Converts boolean values to success/neutral colors
    /// </summary>
    public class BoolToSuccessColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && boolValue)
            {
                return Application.Current.Resources["SuccessGreenBrush"] as SolidColorBrush 
                    ?? new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            
            return Application.Current.Resources["NeutralGrayBrush"] as SolidColorBrush 
                ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts integer values to string with optional parameter suffix
    /// </summary>
    public class IntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue)
            {
                var suffix = parameter?.ToString() ?? string.Empty;
                return $"{intValue}{suffix}";
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue && int.TryParse(stringValue, out int result))
            {
                return result;
            }
            
            return 0;
        }
    }

    /// <summary>
    /// Converts TimeSpan values to formatted time strings
    /// </summary>
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TimeSpan timeSpan)
            {
                return timeSpan.ToString(@"hh\:mm");
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue && TimeSpan.TryParse(stringValue, out TimeSpan result))
            {
                return result;
            }
            
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Converts count values to visibility (visible when count is 0, collapsed otherwise)
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean values to visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            
            return false;
        }
    }

    /// <summary>
    /// Converts data retention days to human-readable text
    /// </summary>
    public class DataRetentionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int days)
            {
                return days switch
                {
                    30 => "1 Month",
                    90 => "3 Months",
                    180 => "6 Months",
                    365 => "1 Year",
                    730 => "2 Years",
                    1095 => "3 Years",
                    _ => $"{days} Days"
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts disruption level number to description
    /// </summary>
    public class DisruptionLevelDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int level)
            {
                return level switch
                {
                    1 => "Toast notifications only",
                    2 => "Enhanced toasts + banners",
                    3 => "Overlays + enhanced disruption",
                    4 => "Maximum disruption (high priority)",
                    _ => "Unknown level"
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts enum values to display strings
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Enum enumValue)
            {
                return enumValue.ToString();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue && targetType.IsEnum)
            {
                return Enum.Parse(targetType, stringValue);
            }
            return value;
        }
    }
} 