using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;

namespace DamageMeter.UI;

// Presentation only: never alters the encounter interval used for damage calculations.
public sealed class ModernFightClock : INotifyPropertyChanged
{
    private readonly Func<DateTime> _now;
    private readonly Func<long> _elapsed;
    private long _sample, _sampledAt, _lastEvent, _encounterStart, _displayTicks;
    private bool _countdown, _canAdvance, _started;
    public bool IsInCombat { get; private set; }
    public string Text { get; private set; } = "00:00.00";
    public Brush StateBrush => IsInCombat ? Brushes.MediumSpringGreen : Brushes.IndianRed;
    public string StatusText => ModernLocalization.Get(IsInCombat ? "In combat" : "Out of combat");
    public event PropertyChangedEventHandler? PropertyChanged;
    public ModernFightClock() : this(() => DateTime.UtcNow,
        () => (long)(Stopwatch.GetTimestamp() * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)) { }
    public ModernFightClock(Func<DateTime> now) : this(now, () => now().Ticks) { }
    public ModernFightClock(Func<DateTime> now, Func<long> elapsed) { _now = now; _elapsed = elapsed; }
    public void Start()
    {
        if (_started) return;
        _started = true;
        CompositionTarget.Rendering += OnRendering;
    }
    public void Stop()
    {
        if (!_started) return;
        CompositionTarget.Rendering -= OnRendering;
        _started = false;
    }
    private void OnRendering(object? sender, EventArgs e) => Refresh();
    public void SetCombat(bool active)
    {
        if (IsInCombat == active) return;
        IsInCombat = active;
        PropertyChanged?.Invoke(this, new(nameof(IsInCombat)));
        PropertyChanged?.Invoke(this, new(nameof(StateBrush)));
        PropertyChanged?.Invoke(this, new(nameof(StatusText)));
        Refresh();
    }
    public void UpdateSample(long ticks, bool countdown, long lastEventTicks, bool paused, long encounterStart = 0)
    {
        ticks = Math.Max(0, ticks);
        var key = encounterStart != 0 ? encounterStart : countdown ? lastEventTicks + ticks : lastEventTicks - ticks;
        var sameEncounter = key == _encounterStart && countdown == _countdown;
        var continuing = sameEncounter && _sample > 0 && lastEventTicks == _lastEvent && _canAdvance;
        var age = _now().Ticks - lastEventTicks;
        var canAdvance = !paused && ticks > 0 && (continuing || Math.Abs(age) < TimeSpan.TicksPerSecond * 5);
        // Identical snapshots retain their monotonic anchor instead of introducing clock jitter.
        if (!continuing || ticks != _sample || canAdvance != _canAdvance)
            _sampledAt = _elapsed() - (canAdvance ? Math.Max(0, age) : 0);
        if (!sameEncounter) _displayTicks = ticks;
        _sample = ticks; _countdown = countdown; _canAdvance = canAdvance;
        _lastEvent = lastEventTicks; _encounterStart = key;
        Refresh();
    }
    public void Refresh()
    {
        var advancing = IsInCombat && _canAdvance;
        var delta = advancing ? Math.Max(0, _elapsed() - _sampledAt) : 0;
        var ticks = Math.Max(0, _sample + (_countdown ? -delta : delta));
        // Delayed samples cannot rewind active combat; idle/history uses the exact sample.
        if (advancing) ticks = _countdown ? Math.Min(_displayTicks, ticks) : Math.Max(_displayTicks, ticks);
        _displayTicks = ticks;
        var time = TimeSpan.FromTicks(ticks);
        var value = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:00}", (long)time.TotalMinutes, time.Seconds, time.Milliseconds / 10);
        if (Text == value) return;
        Text = value; PropertyChanged?.Invoke(this, new(nameof(Text)));
    }
}
