using System;
using System.Globalization;
using System.Windows.Data;

namespace Photobooth.Converters
{
    public class CenterYConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double height && parameter is string elementHeight)
            {
                if (double.TryParse(elementHeight, out double elementHeightValue))
                {
                    return (height - elementHeightValue) / 2;
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 