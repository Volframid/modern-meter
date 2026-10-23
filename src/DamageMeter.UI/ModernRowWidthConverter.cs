using System;
using System.Globalization;
using System.Windows.Data;

namespace DamageMeter.UI;

public sealed class ModernRowWidthConverter : IValueConverter
{
    public static ModernRowWidthConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width ? Math.Max(0, width - 32) : 0d;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
