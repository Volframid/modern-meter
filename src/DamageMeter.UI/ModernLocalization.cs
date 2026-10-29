using System;
using System.Collections;
using System.Collections.Generic;
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
    private static IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>();
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
        var language = ResourceLanguage(culture);
        _dictionary = new ResourceDictionary { Source = new Uri("pack://application:,,,/ModernMeter;component/Resources/ModernStrings." + language + ".xaml") };
        // Native labels used to be x:Static snapshots. Keep them in the same live resource
        // dictionary so open settings, dialogs and templates follow the selected language.
        var native = LP.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (native != null)
            foreach (DictionaryEntry item in native)
                if (item.Key is string key && item.Value is string)
                {
                    var text = LP.ResourceManager.GetString(key, culture) ?? (string)item.Value;
                    // Credits and licenses retain the original project attribution.
                    if (_dictionary.Contains("NativeText_" + key)) continue;
                    _dictionary["NativeText_" + key] = key.Contains("License", StringComparison.OrdinalIgnoreCase)
                        || key.Contains("Author", StringComparison.OrdinalIgnoreCase)
                        || key.Contains("Credit", StringComparison.OrdinalIgnoreCase)
                        ? text : ModernBranding.ProductText(text);
                }
        foreach (var key in new[] { "NativeText_SystemTray_MainWindowOpacity", "NativeText_SystemTray_OtherWindowOpacity" })
            if (_dictionary[key] is string label) _dictionary[key] = label + " (%)";
        application.Resources.MergedDictionaries.Add(_dictionary);
        var texts = new Dictionary<string, string>();
        foreach (var key in _dictionary.Keys)
            if (key is string name && _dictionary[name] is string text) texts[name] = text;
        // Resource keys remain stable when wording is edited. Resolve runtime
        // lookups from the current English wording as well as the original key.
        var english = language == "en" ? _dictionary : new ResourceDictionary
            { Source = new Uri("pack://application:,,,/ModernMeter;component/Resources/ModernStrings.en.xaml") };
        foreach (var key in english.Keys)
            if (key is string name && name.StartsWith("ModernText_") && english[name] is string source)
                texts[Key(source)] = _dictionary[name] as string ?? source;
        System.Threading.Volatile.Write(ref _texts, texts);
        State.Refresh();
    }
    public static string ResourceLanguage(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "ru" or "de" or "fr" or "es" or "it" or "ja" or "ko" => culture.TwoLetterISOLanguageName,
        "pt" => "pt-BR",
        "zh" => culture.Name.Contains("TW", StringComparison.OrdinalIgnoreCase) || culture.Name.Contains("HK", StringComparison.OrdinalIgnoreCase)
            || culture.Name.Contains("MO", StringComparison.OrdinalIgnoreCase) || culture.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "zh-CN",
        _ => "en"
    };
    public static string Get(string text) => System.Threading.Volatile.Read(ref _texts).TryGetValue(Key(text), out var value) ? value : text;
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
