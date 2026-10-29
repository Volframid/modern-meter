using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Data;
using DamageMeter.UI.Broadcast;

namespace DamageMeter.UI;

public static class ModernBroadcast
{
    private static BroadcastPreferences? _preferences;
    private static BroadcastRenderer? _renderer;
    private static NativeBroadcastWindow? _window;
    private static DispatcherTimer? _timer;
    private static bool _initialized, _updating;
    private static int _status, _count;
    private static string _reportedCanvas = "";
    private static long _nextUpdate;
    public static event Action? Changed;
    private static string FileName => Path.Combine(BasicTeraData.Instance.ResourceDirectory, "config", "broadcast.json");
    private static BroadcastPreferences Preferences => _preferences ??= BroadcastPreferences.Load(FileName);
    public static bool Enabled => Preferences.Enabled;
    public static string SourceName => NativeBroadcastWindow.CaptureTitle;
    public static int Status => _status; // 0 disabled, 1 native capture surface ready, 3 unavailable
    public static int WindowCount => _count;
    public static double LastUpdateMilliseconds { get; private set; }
    public static string CanvasSize => _renderer == null ? "—" : $"{_renderer.Canvas.Width:0} × {_renderer.Canvas.Height:0}";

    public static void Initialize()
    {
        if (_initialized) return; _initialized = true;
        _renderer = new BroadcastRenderer();
        // Only position/size metadata is polled. Shared WPF visuals update
        // directly, without bitmap copies, encoding or frame queues.
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateScene();
        _timer.Start(); // Monitor metadata remains current even with streaming disabled.
        if (Enabled) Start();
        Application.Current.Exit += (_, _) => { Stop(); _timer.Stop(); };
    }

    public static void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        Preferences.Enabled = enabled;
        try { Preferences.Save(FileName); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Preferences.Enabled = !enabled; SetStatus(3, _count); return; }
        if (_initialized) { if (enabled) Start(); else Stop(); }
        Changed?.Invoke();
    }

    private static void Start()
    {
        Stop();
        try
        {
            _window = new NativeBroadcastWindow(); UpdateScene(); _window.Show();
            UpdateScene(); CompositionTarget.Rendering += OnRendering; _timer?.Start();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { Stop(); SetStatus(3, 0); }
    }

    private static void Stop()
    {
        CompositionTarget.Rendering -= OnRendering;
        var window = _window; _window = null; window?.Close();
        _renderer?.Clear(); _nextUpdate = 0; SetStatus(0, 0);
    }

    private static void OnRendering(object? sender, EventArgs args)
    {
        var now = Stopwatch.GetTimestamp();
        if (now < _nextUpdate) return;
        _nextUpdate = now + Stopwatch.Frequency / 60; UpdateScene();
    }

    private static void UpdateScene()
    {
        if (_updating || _renderer == null) return;
        if (_window == null) { _renderer.RefreshCanvas(); SetStatus(_status, 0); return; }
        _updating = true; var started = Stopwatch.GetTimestamp();
        try
        {
            var layers = _renderer.Scan();
            _window.Update(_renderer.Canvas, layers); SetStatus(1, layers.Count);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { SetStatus(3, _count); }
        finally { LastUpdateMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds; _updating = false; }
    }

    private static void SetStatus(int status, int count)
    {
        var canvas = CanvasSize;
        if (_status == status && _count == count && _reportedCanvas == canvas) return;
        _status = status; _count = count; _reportedCanvas = canvas; Changed?.Invoke();
    }
}
