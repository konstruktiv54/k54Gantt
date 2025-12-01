using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wpf.Converters;

public class TimeSpanToDateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not DateTime projectStart)
            return DateTime.Today;

        // Обработка null и пустых значений
        if (values[0] == null || values[0] == DependencyProperty.UnsetValue)
            return null!;

        // Единая проверка для TimeSpan и TimeSpan?
        if (values[0] is TimeSpan timeSpan)
        {
            return projectStart + timeSpan;
        }

        return null!;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return new object[] { Binding.DoNothing, Binding.DoNothing };
    }
}

public class TimeSpanDateConverter : IValueConverter
{
    public static DateTime ProjectStart { get; set; } = DateTime.Today;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Единая проверка обрабатывает и TimeSpan, и TimeSpan?
        if (value is TimeSpan timeSpan)
        {
            return ProjectStart + timeSpan;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime selectedDate)
        {
            var timeSpan = selectedDate - ProjectStart;
            return timeSpan < TimeSpan.Zero ? TimeSpan.Zero : timeSpan;
        }

        return targetType == typeof(TimeSpan?) 
            ? null 
            : (object?)TimeSpan.Zero;
    }
}