using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Threading;
using System.Xml.Linq;
using Data;

namespace DamageMeter.UI;

// Compatibility with the native profile writer, which reads opacity as an
// integer percentage. The small UI sidecar also makes slot migration one-time.
public static class ModernPreferences
{
    public sealed class State { public int LayoutVersion { get; set; } public double? OverlayOpacity { get; set; } }
    private static readonly ConditionalWeakTable<WindowData, State> States = new();
    private static DispatcherTimer? _saveTimer;
    private static string FileName => Path.Combine(BasicTeraData.Instance.ResourceDirectory, "config", "modern-ui.json");

    public static void Initialize(WindowData data)
    {
        if (States.TryGetValue(data, out _)) return;
        State state;
        try { state = File.Exists(FileName) ? JsonSerializer.Deserialize<State>(File.ReadAllText(FileName)) ?? new State() : new State(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { state = new State(); }
        if (state.OverlayOpacity is double opacity && double.IsFinite(opacity)) data.MainWindowOpacity = Math.Clamp(opacity, .05, 1);
        else
        {
            // Use the already-loaded XML; the native owner keeps its file locked.
            var xml = typeof(WindowData).GetField("_xml", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(data) as XDocument;
            var text = xml?.Root?.Element("opacity")?.Element("mainWindow")?.Value;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && double.IsFinite(percent))
                data.MainWindowOpacity = Math.Clamp(percent / 100, .05, 1);
        }
        if (state.LayoutVersion < 1)
        {
            foreach (var metrics in new[] { data.DpsMetrics, data.TankMetrics, data.HealerMetrics })
            {
                if (metrics[3] == Metric.Deaths)
                {
                    metrics[3] = Metric.None;
                    metrics[4] = Metric.Deaths;
                }
                else if (metrics[4] == Metric.None) metrics[4] = Metric.Deaths;
            }
            state.LayoutVersion = 1;
            data.Save();
        }
        States.Add(data, state);
        SaveUi(data, state);
    }

    public static void ScheduleSave()
    {
        _saveTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => Flush(), Dispatcher.CurrentDispatcher);
        _saveTimer.Stop(); _saveTimer.Start();
    }

    public static void Flush()
    {
        _saveTimer?.Stop();
        var data = BasicTeraData.Instance.WindowData;
        Initialize(data);
        SaveUi(data, States.GetValue(data, _ => new State()));
        data.Save();
    }

    private static void SaveUi(WindowData data, State state)
    {
        state.OverlayOpacity = data.MainWindowOpacity;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            File.WriteAllText(FileName + ".tmp", JsonSerializer.Serialize(state));
            File.Move(FileName + ".tmp", FileName, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { BasicTeraData.LogError("Could not save Modern Meter UI preferences.", true, true); }
    }
}
