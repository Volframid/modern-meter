using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace DamageMeter.UI.Broadcast;

/// <summary>A single native capture surface containing live, shared WPF visuals.</summary>
public sealed class NativeBroadcastWindow : Window
{
    public const string CaptureTitle = "Modern Meter — Stream";
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent, ClipToBounds = true };
    private readonly Dictionary<string, Rectangle> _mirrors = new();
    private readonly Dictionary<string, BroadcastContentProjection> _projections = new();
    private BroadcastBounds _bounds = new(0, 0, 1, 1);
    public int LayerCount => _mirrors.Count;

    static NativeBroadcastWindow()
    {
        TopmostProperty.OverrideMetadata(typeof(NativeBroadcastWindow),
            new FrameworkPropertyMetadata(false, null, (_, _) => false));
        ShowInTaskbarProperty.OverrideMetadata(typeof(NativeBroadcastWindow),
            new FrameworkPropertyMetadata(false, null, (_, _) => false));
    }

    public NativeBroadcastWindow()
    {
        Title = CaptureTitle; Icon = ModernDamageMark.ApplicationImage;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowActivated = false; ShowInTaskbar = false; Focusable = false;
        Left = -32000; Top = -32000; Width = 1; Height = 1;
        Content = new Viewbox { Stretch = Stretch.Fill, Child = _canvas, IsHitTestVisible = false };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            // OBS/Streamlabs exclude tool windows; keep an ordinary capture target.
            var style = GetWindowLong(handle, -20);
            SetWindowLong(handle, -20, (style & ~0x80) | 0x08000000 | 0x20);
            HwndSource.FromHwnd(handle)?.AddHook(ProtectBackgroundOrder);
            PlaceCaptureSurface(); KeepBehindApplications();
        };
        Loaded += (_, _) => { PlaceCaptureSurface(); KeepBehindApplications(); };
        ContentRendered += (_, _) => KeepBehindApplications();
        Activated += (_, _) => KeepBehindApplications();
        Closed += (_, _) => Clear();
    }

    public void Update(BroadcastBounds canvas, IReadOnlyList<BroadcastVisual> layers)
    {
        _canvas.Width = canvas.Width; _canvas.Height = canvas.Height;
        _bounds = canvas;
        var available = Math.Max(0, canvas.Width - 1);
        var clip = new Rect(0, 0, available, canvas.Height);
        if (_canvas.Clip is not RectangleGeometry oldClip || oldClip.Rect != clip)
        { var geometry = new RectangleGeometry(clip); geometry.Freeze(); _canvas.Clip = geometry; }
        // Keep content outside all monitors. One transparent edge pixel keeps
        // Windows composing the surface without displaying a second local Meter.
        if (new WindowInteropHelper(this).Handle == IntPtr.Zero)
        { var anchor = Anchor(); Left = anchor.X - canvas.Width + 1; Top = anchor.Y; Width = canvas.Width; Height = canvas.Height; }
        else PlaceCaptureSurface();
        var seen = new HashSet<string>();
        foreach (var layer in layers)
        {
            if (ReferenceEquals(layer.Root, this)) continue;
            if (!_mirrors.TryGetValue(layer.Id, out var mirror))
            {
                mirror = new Rectangle { IsHitTestVisible = false };
                _mirrors.Add(layer.Id, mirror); _canvas.Children.Add(mirror);
            }
            Visual visual = layer.Root;
            if (layer.Root is Window sourceWindow)
            {
                if (sourceWindow.Content is not FrameworkElement content) continue;
                if (!_projections.TryGetValue(layer.Id, out var projection))
                    _projections.Add(layer.Id, projection = new BroadcastContentProjection());
                visual = projection.Update(sourceWindow, content);
                mirror.Opacity = layer.Opacity;
            }
            else mirror.Opacity = 1;
            var brush = mirror.Fill as VisualBrush;
            if (brush == null || !ReferenceEquals(brush.Visual, visual))
                mirror.Fill = brush = new VisualBrush
                { AutoLayoutContent = false, Visual = visual, Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute };
            var viewbox = new Rect(layer.Root.RenderSize);
            if (brush.Viewbox != viewbox) brush.Viewbox = viewbox;
            // Window opacity is applied by the destination rectangle exactly once.
            var bounds = layer.Bounds;
            var fit = Math.Min(1, Math.Min(available / Math.Max(1, bounds.Width), canvas.Height / Math.Max(1, bounds.Height)));
            mirror.Width = bounds.Width * fit; mirror.Height = bounds.Height * fit;
            Canvas.SetLeft(mirror, Math.Clamp(bounds.X, 0, Math.Max(0, available - mirror.Width)));
            Canvas.SetTop(mirror, Math.Clamp(bounds.Y, 0, Math.Max(0, canvas.Height - mirror.Height)));
            Panel.SetZIndex(mirror, layer.Z); seen.Add(layer.Id);
        }
        foreach (var id in _mirrors.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            var mirror = _mirrors[id];
            if (mirror.Fill is VisualBrush brush) brush.Visual = null;
            _canvas.Children.Remove(mirror); _mirrors.Remove(id);
            if (_projections.Remove(id, out var projection)) projection.Dispose();
        }
    }

    private void PlaceCaptureSurface()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var width = Math.Max(1, (int)Math.Round(_bounds.Width));
        var height = Math.Max(1, (int)Math.Round(_bounds.Height));
        var anchor = Anchor(); var x = anchor.X - width + 1; var y = anchor.Y;
        if (!GetWindowRect(handle, out var rect) || rect.Left != x || rect.Top != y ||
            rect.Right - rect.Left != width || rect.Bottom - rect.Top != height)
            SetWindowPos(handle, new IntPtr(1), x, y, width, height, 0x0210);
    }

    private static System.Drawing.Point Anchor()
    {
        var monitor = Forms.Screen.AllScreens.OrderBy(screen => screen.Bounds.Left)
            .ThenBy(screen => screen.Bounds.Top).FirstOrDefault();
        return monitor?.Bounds.Location ?? System.Drawing.Point.Empty;
    }

    private void KeepBehindApplications()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero) SetWindowPos(handle, new IntPtr(1), 0, 0, 0, 0, 0x0213);
    }

    private IntPtr ProtectBackgroundOrder(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0046 && lParam != IntPtr.Zero) // WM_WINDOWPOSCHANGING
        {
            var position = Marshal.PtrToStructure<NativeWindowPosition>(lParam);
            if ((position.Flags & 0x0004) == 0) // SWP_NOZORDER
            {
                position.After = new IntPtr(1); // HWND_BOTTOM
                Marshal.StructureToPtr(position, lParam, false);
            }
        }
        return IntPtr.Zero;
    }

    public void Clear()
    {
        foreach (var mirror in _mirrors.Values)
            if (mirror.Fill is VisualBrush brush) brush.Visual = null;
        _mirrors.Clear(); _canvas.Children.Clear();
        foreach (var projection in _projections.Values) projection.Dispose();
        _projections.Clear();
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeWindowPosition
    {
        public IntPtr Window, After;
        public int X, Y, Width, Height;
        public uint Flags;
    }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
}
