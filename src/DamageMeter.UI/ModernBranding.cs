using System;
using System.Reflection;

namespace DamageMeter.UI;

/// <summary>Product identity and backwards-compatible normalization of legacy labels.</summary>
public static class ModernBranding
{
    public const string Name = "Modern Meter";
    public const string ProjectUrl = "https://github.com/Volframid/modern-meter";
    public static string ProjectLink(string? type) => ProjectUrl + (type switch
    {
        "wiki" => "#readme",
        "patch" => "/releases",
        "issues" => "/issues",
        _ => ""
    });
    public static string Version { get; } = new Version(typeof(ModernBranding).Assembly
        .GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version).ToString(3);
    public static string WindowTitle => Name + " v" + Version;

    public static string ProductText(string? text) => (text ?? "")
        .Replace("Shinra Meter", Name, StringComparison.OrdinalIgnoreCase)
        .Replace("ShinraMeter", Name, StringComparison.OrdinalIgnoreCase);
}
