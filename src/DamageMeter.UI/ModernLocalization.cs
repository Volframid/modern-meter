using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace DamageMeter.UI;

public static class ModernLocalization
{
    private static ResourceDictionary? _dictionary;
    public static string Key(string text) => "ModernText_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).Substring(0, 12);
    public static CultureInfo Resolve(string? saved, CultureInfo windowsCulture) =>
        string.IsNullOrWhiteSpace(saved) || saved == "Auto" ? windowsCulture : CultureInfo.GetCultureInfo(saved);
    public static void Apply(CultureInfo culture)
    {
        var application = Application.Current;
        if (application == null) return;
        if (_dictionary != null) application.Resources.MergedDictionaries.Remove(_dictionary);
        var language = culture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        _dictionary = new ResourceDictionary { Source = new Uri("pack://application:,,,/ShinraMeter;component/Resources/ModernStrings." + language + ".xaml") };
        application.Resources.MergedDictionaries.Add(_dictionary);
    }
    public static string Get(string text) => Application.Current?.TryFindResource(Key(text)) as string ?? text;
}

public sealed class ModernEnumDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return "";
        var member = value.GetType().GetField(value.ToString() ?? "");
        var description = member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString() ?? "";
        return ModernLocalization.Get(description);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ModernMetricLabelConverter : IValueConverter
{
    public string Prefix { get; set; } = "";
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ModernLocalization.Get(Prefix) + " " + value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
