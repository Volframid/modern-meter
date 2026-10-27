using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Lang;
using Data;

namespace DamageMeter.UI;

public static class ModernLocalization
{
    private static ResourceDictionary? _dictionary;
    public static CultureInfo WindowsCulture { get; } = CultureInfo.CurrentUICulture;
    public static LocalizationState State { get; } = new();
    public static string Key(string text) => "ModernText_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).Substring(0, 12);
    public static CultureInfo Resolve(string? saved, CultureInfo windowsCulture) =>
        string.IsNullOrWhiteSpace(saved) || saved == "Auto" ? windowsCulture : CultureInfo.GetCultureInfo(saved);
    public static void Apply(CultureInfo culture)
    {
        var application = Application.Current;
        if (application == null) return;
        LP.Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        FormatHelpers.Instance.CultureInfo = culture;
        if (_dictionary != null) application.Resources.MergedDictionaries.Remove(_dictionary);
        var language = culture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        _dictionary = new ResourceDictionary { Source = new Uri("pack://application:,,,/ShinraMeter;component/Resources/ModernStrings." + language + ".xaml") };
        // Native labels used to be x:Static snapshots. Keep them in the same live resource
        // dictionary so open settings, dialogs and templates follow the selected language.
        var native = LP.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (native != null)
            foreach (DictionaryEntry item in native)
                if (item.Key is string key && item.Value is string)
                {
                    var text = LP.ResourceManager.GetString(key, culture) ?? (string)item.Value;
                    // Credits and licenses retain the original project attribution.
                    _dictionary["NativeText_" + key] = key.Contains("License", StringComparison.OrdinalIgnoreCase)
                        || key.Contains("Author", StringComparison.OrdinalIgnoreCase)
                        || key.Contains("Credit", StringComparison.OrdinalIgnoreCase)
                        ? text : ModernBranding.ProductText(text);
                }
        application.Resources.MergedDictionaries.Add(_dictionary);
        State.Refresh();
    }
    public static string Get(string text) => Application.Current?.TryFindResource(Key(text)) as string ?? text;
}

public sealed class LocalizationState : INotifyPropertyChanged
{
    public int Version { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Refresh() { Version++; PropertyChanged?.Invoke(this, new(nameof(Version))); }
}

// Include the language revision in translated value bindings, even when the metric itself
// has not changed (for example a paused encounter or an open metrics dropdown).
public sealed class LocalizedValueExtension : MarkupExtension
{
    public string? Path { get; set; }
    public IValueConverter Converter { get; set; } = null!;
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding { Mode = BindingMode.OneWay, Converter = new LocalizedValueConverter(Converter) };
        binding.Bindings.Add(Path == null ? new Binding() : new Binding(Path));
        binding.Bindings.Add(new Binding(nameof(LocalizationState.Version)) { Source = ModernLocalization.State });
        return binding.ProvideValue(serviceProvider);
    }
    private sealed class LocalizedValueConverter(IValueConverter converter) : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values[0] == DependencyProperty.UnsetValue ? "" : converter.Convert(values[0], targetType, parameter, LP.Culture);
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
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
