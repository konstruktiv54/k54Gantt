using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Wpf.Converters;

/// <summary>
/// Конвертер для цвета текста дедлайна (красный если просрочено).
/// </summary>
public class BoolToOverdueColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOverdue && isOverdue)
        {
            return new SolidColorBrush(Color.FromRgb(211, 47, 47)); // Red 700
        }
        
        return new SolidColorBrush(Color.FromRgb(66, 66, 66)); // Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер BoolToVisibility с поддержкой инверсии через ConverterParameter.
/// </summary>
public class BoolToVisibilityConverterEx : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        
        // Инверсия если parameter = "Inverse"
        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            boolValue = !boolValue;
        }
        
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер null → Visibility (Collapsed если null).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value == null || (value is string s && string.IsNullOrEmpty(s));
        
        // Инверсия если parameter = "Inverse"
        if (parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }
        
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}