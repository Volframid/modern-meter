using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DamageMeter.UI;

public sealed class ModernClassBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Colors = new(StringComparer.OrdinalIgnoreCase);

    static ModernClassBrushConverter()
    {
        foreach (var pair in new[] {
            ("Warrior", "#F4A45E"), ("Lancer", "#58B7F6"), ("Slayer", "#FFCF6E"),
            ("Berserker", "#EB6F79"), ("Sorcerer", "#9A9DFF"), ("Archer", "#91CA68"),
            ("Priest", "#80DFB5"), ("Mystic", "#BB94F8"), ("Elementalist", "#BB94F8"),
            ("Reaper", "#CE7EC6"), ("Gunner", "#73D6D9"), ("Engineer", "#73D6D9"),
            ("Brawler", "#E5A77C"), ("Fighter", "#E5A77C"), ("Ninja", "#F077A1"),
            ("Valkyrie", "#B7BDEE"), ("ValkyrieFemale", "#B7BDEE"), ("Common", "#A7AFC4") })
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Item2));
            brush.Freeze();
            Colors[pair.Item1] = brush;
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Colors.TryGetValue(value?.ToString() ?? "Common", out var brush) ? brush : Colors["Common"];

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
