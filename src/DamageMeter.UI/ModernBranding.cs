using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DamageMeter.UI;

/// <summary>Visible product identity; internal assembly and protocol names remain compatible.</summary>
public static class ModernBranding
{
    public const string Name = "Modern Meter";
    private static readonly Lazy<string> ProductVersion = new(ReadVersion);
    public static string Version => ProductVersion.Value;
    public static string WindowTitle => Name + " v" + Version;

    private static string ReadVersion()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "module.json");
            if (File.Exists(file))
            {
                var version = (string?)JObject.Parse(File.ReadAllText(file))["version"];
                if (System.Version.TryParse(version, out _)) return version!;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Newtonsoft.Json.JsonException) { }
        return "1.0.6";
    }

    public static string ProductText(string? text) => (text ?? "")
        .Replace("Shinra Meter", Name, StringComparison.OrdinalIgnoreCase)
        .Replace("ShinraMeter", Name, StringComparison.OrdinalIgnoreCase);
}
