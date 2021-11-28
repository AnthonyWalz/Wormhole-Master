using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using Wormhole.ViewModels;

namespace Wormhole.Views.Converters
{
    [ValueConversion(typeof(DestinationViewModel), typeof(string))]
    public class DestinationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value?.GetType().GetCustomAttribute<DestinationAttribute>()?.Type is { } type)
                return type.Description();

            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}