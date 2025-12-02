using System;
using System.Globalization;
using System.Windows.Data;
using Core.Models;

namespace Wpf.Converters
{
    public class EnumToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ResourceRole role)
            {
                return role.GetDisplayName();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}