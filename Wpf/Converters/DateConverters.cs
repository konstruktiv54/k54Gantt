// GanttChart.WPF/Converters/DateConverters.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wpf.Converters;

/// <summary>
/// Конвертирует TimeSpan (смещение от начала проекта) в DateTime и обратно.
/// Использует MultiBinding для получения ProjectStart из ViewModel.
/// </summary>
/// <remarks>
/// Использование в XAML:
/// <code>
/// &lt;DatePicker&gt;
///   &lt;DatePicker.SelectedDate&gt;
///     &lt;MultiBinding Converter="{StaticResource TimeSpanToDateMultiConverter}"&gt;
///       &lt;Binding Path="Start" Mode="TwoWay"/&gt;
///       &lt;Binding Path="DataContext.ResourceViewModel.ProjectStart" 
///                RelativeSource="{RelativeSource AncestorType=Window}"/&gt;
///     &lt;/MultiBinding&gt;
///   &lt;/DatePicker.SelectedDate&gt;
/// &lt;/DatePicker&gt;
/// </code>
/// </remarks>
public class TimeSpanToDateMultiConverter : IMultiValueConverter
{
    /// <summary>
    /// Конвертирует TimeSpan + ProjectStart в DateTime для DatePicker.
    /// </summary>
    /// <param name="values">
    /// [0] — TimeSpan или TimeSpan? (смещение от начала проекта)
    /// [1] — DateTime (дата начала проекта)
    /// </param>
    /// <returns>DateTime для отображения в DatePicker, или null для пустого поля.</returns>
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Проверка входных данных
        if (values.Length < 2)
            return null;

        // ProjectStart обязателен
        if (values[1] is not DateTime projectStart)
            return null;

        // Кэшируем ProjectStart для использования в ConvertBack
        _cachedProjectStart = projectStart;

        // Обработка UnsetValue (binding ещё не готов)
        if (values[0] == DependencyProperty.UnsetValue)
            return null;

        // Обработка null (для nullable TimeSpan?, например ParticipationInterval.End)
        if (values[0] == null)
            return null;

        // Основная конвертация
        if (values[0] is TimeSpan timeSpan)
        {
            return projectStart + timeSpan;
        }

        return null;
    }

    /// <summary>
    /// Конвертирует выбранную дату обратно в TimeSpan.
    /// </summary>
    /// <param name="value">DateTime из DatePicker или null.</param>
    /// <param name="targetTypes">Типы целевых свойств [TimeSpan/TimeSpan?, DateTime].</param>
    /// <returns>
    /// [0] — TimeSpan/null для записи в модель
    /// [1] — Binding.DoNothing (ProjectStart не изменяется)
    /// </returns>
    public object?[] ConvertBack(object? value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        // Получаем ProjectStart из parameter (передаётся через ConverterParameter)
        // Но в MultiBinding это не работает напрямую, поэтому используем workaround:
        // ProjectStart кэшируется при последнем Convert вызове
        
        // Если DatePicker очищен — возвращаем null (для nullable TimeSpan?)
        if (value == null)
        {
            // Проверяем, ожидается ли nullable тип
            if (targetTypes.Length > 0 && IsNullableTimeSpan(targetTypes[0]))
            {
                return [null, Binding.DoNothing];
            }
            // Для non-nullable возвращаем DoNothing (не меняем значение)
            return [Binding.DoNothing, Binding.DoNothing];
        }

        if (value is not DateTime selectedDate)
        {
            return [Binding.DoNothing, Binding.DoNothing];
        }

        // Используем кэшированный ProjectStart
        var timeSpan = selectedDate - _cachedProjectStart;
        
        // Валидация: дата не может быть раньше начала проекта
        if (timeSpan < TimeSpan.Zero)
        {
            timeSpan = TimeSpan.Zero;
        }

        return [timeSpan, Binding.DoNothing];
    }

    /// <summary>
    /// Кэш ProjectStart для использования в ConvertBack.
    /// Обновляется при каждом вызове Convert.
    /// </summary>
    private DateTime _cachedProjectStart = DateTime.Today;

    /// <summary>
    /// Проверяет, является ли тип Nullable&lt;TimeSpan&gt;.
    /// </summary>
    private static bool IsNullableTimeSpan(Type type)
    {
        return type == typeof(TimeSpan?) || 
               (type.IsGenericType && 
                type.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                type.GetGenericArguments()[0] == typeof(TimeSpan));
    }
}

/// <summary>
/// Конвертирует TimeSpan в строку даты для отображения в TextBlock.
/// Использует MultiBinding для получения ProjectStart из ViewModel.
/// </summary>
/// <remarks>
/// Поддерживает:
/// - TimeSpan → "dd.MM.yyyy"
/// - null (TimeSpan?) → "∞" или пустая строка
/// - Кастомный формат через ConverterParameter
/// </remarks>
public class TimeSpanToDateStringMultiConverter : IMultiValueConverter
{
    /// <summary>
    /// Текст для отображения null значения (бессрочный интервал).
    /// </summary>
    public string NullDisplayText { get; set; } = "∞";

    /// <summary>
    /// Формат даты по умолчанию.
    /// </summary>
    public string DefaultDateFormat { get; set; } = "dd.MM.yyyy";

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Проверка входных данных
        if (values.Length < 2)
            return string.Empty;

        // ProjectStart обязателен
        if (values[1] is not DateTime projectStart)
            return string.Empty;

        // Обработка UnsetValue
        if (values[0] == DependencyProperty.UnsetValue)
            return string.Empty;

        // Обработка null (для nullable TimeSpan?)
        if (values[0] == null)
            return NullDisplayText;

        // Основная конвертация
        if (values[0] is TimeSpan timeSpan)
        {
            var date = projectStart + timeSpan;
            var format = parameter as string ?? DefaultDateFormat;
            return date.ToString(format, culture);
        }

        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        // Только для отображения, обратная конвертация не поддерживается
        return [Binding.DoNothing, Binding.DoNothing];
    }
}

/// <summary>
/// Конвертирует DateTime в строку заданного формата.
/// </summary>
public class DateToStringConverter : IValueConverter
{
    /// <summary>
    /// Формат даты по умолчанию.
    /// </summary>
    public string DefaultFormat { get; set; } = "dd.MM.yyyy";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime date)
        {
            var format = parameter as string ?? DefaultFormat;
            return date.ToString(format, culture);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && DateTime.TryParse(str, culture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        return DependencyProperty.UnsetValue;
    }
}