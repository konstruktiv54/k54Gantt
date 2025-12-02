using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Core.Models;

namespace Wpf.Converters;

/// <summary>
/// Конвертер DayState в цвет заливки.
/// </summary>
public class DayStateToColorConverter : IMultiValueConverter
{
    // Статические цвета для состояний
    private static readonly SolidColorBrush FreeBrush = new(Color.FromRgb(245, 245, 245));
    private static readonly SolidColorBrush AbsenceBrush = new(Color.FromRgb(189, 189, 189));
    private static readonly SolidColorBrush NotParticipatingBrush = new(Color.FromRgb(224, 224, 224));
    private static readonly SolidColorBrush OverbookedBorderBrush = new(Color.FromRgb(211, 47, 47));

    /// <summary>
    /// Конвертирует DayState и цвет ресурса в кисть заливки.
    /// values[0] = DayState
    /// values[1] = ResourceColorHex (string)
    /// values[2] = AllocationPercent (int)
    /// values[3] = MaxWorkload (int)
    /// </summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not DayState state)
            return FreeBrush;

        var resourceColorHex = values[1] as string ?? "#4682B4";

        switch (state)
        {
            case DayState.Free:
                return FreeBrush;

            case DayState.Absence:
                return AbsenceBrush;

            case DayState.NotParticipating:
                return NotParticipatingBrush;

            case DayState.PartialAssigned:
            case DayState.Assigned:
            case DayState.Overbooked:
                // Используем цвет ресурса с прозрачностью для PartialAssigned
                var baseColor = ParseColor(resourceColorHex);
                
                if (state == DayState.PartialAssigned && values.Length >= 4)
                {
                    var allocation = values[2] is int a ? a : 50;
                    var maxWorkload = values[3] is int m && m > 0 ? m : 100;
                    var opacity = Math.Min(1.0, (double)allocation / maxWorkload);
                    // Минимум 30% opacity чтобы было видно
                    opacity = 0.3 + opacity * 0.7;
                    baseColor.A = (byte)(255 * opacity);
                }

                return new SolidColorBrush(baseColor);

            default:
                return FreeBrush;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length == 6)
            {
                return Color.FromRgb(
                    System.Convert.ToByte(hex.Substring(0, 2), 16),
                    System.Convert.ToByte(hex.Substring(2, 2), 16),
                    System.Convert.ToByte(hex.Substring(4, 2), 16));
            }
        }
        catch { }

        return Color.FromRgb(70, 130, 180); // SteelBlue fallback
    }
}

/// <summary>
/// Простой конвертер DayState → цвет (без учёта цвета ресурса).
/// </summary>
public class SimpleDayStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DayState state)
            return Brushes.Transparent;

        return state switch
        {
            DayState.Free => new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            DayState.Absence => new SolidColorBrush(Color.FromRgb(189, 189, 189)),
            DayState.NotParticipating => new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            DayState.PartialAssigned => new SolidColorBrush(Color.FromRgb(144, 202, 249)),
            DayState.Assigned => new SolidColorBrush(Color.FromRgb(66, 165, 245)),
            DayState.Overbooked => new SolidColorBrush(Color.FromRgb(239, 154, 154)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}