using System;
using System.Globalization;
using System.Windows.Data;

namespace Photobooth.Converters
{
    public class CenterXConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string elementWidth)
            {
                if (double.TryParse(elementWidth, out double elementWidthValue))
                {
                    return (width - elementWidthValue) / 2;
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