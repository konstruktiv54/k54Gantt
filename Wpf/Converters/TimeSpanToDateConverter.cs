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
        if (values[0] == DependencyProperty.UnsetValue)
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
        return [Binding.DoNothing, Binding.DoNothing];
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
            DateTime date = ProjectStart + timeSpan;
            
            // Если передали формат через parameter, используем его
            if (parameter is string format)
            {
                return date.ToString(format);
            }
            
            // По умолчанию возвращаем дату в формате "дд/мм/гггг"
            return date.ToString("dd/MM/yyyy");
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime selectedDate)
            return targetType == typeof(TimeSpan?)
                ? null
                : TimeSpan.Zero;
        
        var timeSpan = selectedDate - ProjectStart;
        return timeSpan < TimeSpan.Zero ? TimeSpan.Zero : timeSpan;
    }
}