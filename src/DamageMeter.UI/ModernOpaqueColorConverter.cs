using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace DamageMeter.UI;

/// <summary>Keep menu surfaces readable without changing the user's overlay color.</summary>
public sealed class ModernOpaqueColorConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Color color ? Color.FromRgb(color.R, color.G, color.B) : Colors.Black;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
