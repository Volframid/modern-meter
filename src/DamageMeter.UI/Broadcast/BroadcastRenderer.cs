using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace DamageMeter.UI.Broadcast;

/// <summary>Renders our process's WPF visuals, never desktop pixels or another application.</summary>
public sealed record BroadcastVisual(string Id, FrameworkElement Root, BroadcastBounds Bounds, double Opacity, int Z);

public sealed class BroadcastRenderer : IDisposable
{
    private readonly Dictionary<IntPtr, Surface> _surfaces = new();
    private readonly uint _process = (uint)Environment.ProcessId;
    private long _version;
    public BroadcastBounds Canvas { get; private set; } = new(0, 0, 0, 0);

    public BroadcastRenderer() { RefreshCanvas(); }

    public void RefreshCanvas()
    {
        var main = Application.Current?.MainWindow;
        var handle = main != null && main.Dispatcher.CheckAccess() ? new WindowInteropHelper(main).Handle : IntPtr.Zero;
        var monitor = handle != IntPtr.Zero ? Forms.Screen.FromHandle(handle) : Forms.Screen.PrimaryScreen;
        var rect = monitor?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        Canvas = new BroadcastBounds(rect.X, rect.Y, rect.Width, rect.Height);
    }

    public IReadOnlyList<BroadcastVisual> Scan()
    {
        RefreshCanvas();
        var order = new Dictionary<IntPtr, int>(); var z = 100000;
        EnumWindows((window, _) => { order[window] = z--; return true; }, IntPtr.Zero);
        var sources = PresentationSource.CurrentSources.OfType<HwndSource>().Where(source => source.Dispatcher.CheckAccess()).ToArray();
        var roots = sources.Where(source => !source.IsDisposed && source.RootVisual is Window)
            .ToDictionary(source => source.Handle, source => (Window)source.RootVisual);
        var layers = new List<BroadcastVisual>();
        foreach (var source in sources)
        {
            try
            {
                if (source.IsDisposed || source.Handle == IntPtr.Zero || !IsWindowVisible(source.Handle) || IsIconic(source.Handle)) continue;
                GetWindowThreadProcessId(source.Handle, out var process);
                if (process != _process || source.RootVisual is not FrameworkElement root || !root.Dispatcher.CheckAccess()) continue;
                if (root is NativeBroadcastWindow) continue; // Never mirror the destination into itself.
                var window = root as Window;
                if (window != null && (window.WindowState == WindowState.Minimized || window.Opacity <= 0)) continue;
                // Capture the HWND's root. Capturing transformed Content with
                // its untransformed RenderSize clips the right/bottom at UI scales >100%.
                var visual = root;
                if (visual.RenderSize.Width < 1 || visual.RenderSize.Height < 1) continue;
                var a = visual.PointToScreen(new Point(0, 0));
                var b = visual.PointToScreen(new Point(visual.RenderSize.Width, visual.RenderSize.Height));
                var width = b.X - a.X; var height = b.Y - a.Y;
                if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1) continue;
                var owner = window == null ? GetWindow(source.Handle, 4) : source.Handle;
                var monitor = Forms.Screen.FromHandle(roots.ContainsKey(owner) ? owner : source.Handle).Bounds;
                var bounds = BroadcastFrames.Fold(new BroadcastBounds(a.X, a.Y, width, height),
                    new BroadcastBounds(monitor.X, monitor.Y, monitor.Width, monitor.Height), Canvas);
                var id = "w" + source.Handle.ToInt64().ToString("x");
                layers.Add(new BroadcastVisual(id, visual, bounds, window?.Opacity ?? 1,
                    order.TryGetValue(source.Handle, out var level) ? level : 200000));
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or COMException) { }
        }
        return layers;
    }

    // Bitmap capture is retained only for compatibility diagnostics. The native
    // stream host consumes Scan() directly and never calls this pixel-copy path.
    public IReadOnlyList<BroadcastLayer> Capture()
    {
        var visuals = Scan(); var layers = new List<BroadcastLayer>();
        var seen = new HashSet<IntPtr>();
        foreach (var visual in visuals)
        {
            var handle = new IntPtr(Convert.ToInt64(visual.Id.Substring(1), 16));
            if (!_surfaces.TryGetValue(handle, out var surface)) _surfaces[handle] = surface = new Surface();
            layers.Add(surface.Render(visual.Id, visual.Root, visual.Bounds, visual.Opacity, visual.Z, ref _version));
            seen.Add(handle);
        }
        foreach (var handle in _surfaces.Keys.Where(handle => !seen.Contains(handle)).ToArray())
        { _surfaces[handle].Dispose(); _surfaces.Remove(handle); }
        return layers;
    }

    public void Clear() { foreach (var item in _surfaces.Values) item.Dispose(); _surfaces.Clear(); }
    public void Dispose() => Clear();

    // Also used by isolated fixture tests: no HWND, production App or input required.
    public sealed class Surface : IDisposable
    {
        private RenderTargetBitmap? _bitmap;
        private byte[]? _pixels;
        private byte[]? _hash;
        private byte[] _png = Array.Empty<byte>();
        private long _version;

        public BroadcastLayer Render(string id, FrameworkElement visual, BroadcastBounds bounds, double opacity, int z, ref long version)
        {
            var width = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, 8192);
            var height = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, 8192);
            var reduction = Math.Min(1, Math.Sqrt(16_777_216d / ((double)width * height)));
            width = Math.Max(1, (int)(width * reduction)); height = Math.Max(1, (int)(height * reduction));
            var size = checked(width * height * 4);
            if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
            {
                Dispose(); _bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                _pixels = ArrayPool<byte>.Shared.Rent(size); _hash = null;
            }
            _bitmap.Clear();
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                var brush = new VisualBrush(visual) { AutoLayoutContent = false, Stretch = Stretch.Fill,
                    ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, visual.RenderSize.Width, visual.RenderSize.Height) };
                context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }
            _bitmap.Render(drawing); _bitmap.CopyPixels(_pixels!, width * 4, 0);
            var hash = SHA256.HashData(_pixels!.AsSpan(0, size));
            if (_hash == null || !hash.AsSpan().SequenceEqual(_hash))
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                using var stream = new MemoryStream(); encoder.Save(stream); _png = stream.ToArray();
                _hash = hash; _version = ++version;
            }
            return new BroadcastLayer(id, bounds, opacity, z, _version, _png);
        }

        public void Dispose()
        {
            if (_pixels != null) { ArrayPool<byte>.Shared.Return(_pixels); _pixels = null; }
            _bitmap = null; _hash = null; _png = Array.Empty<byte>();
        }
    }

    private delegate bool EnumWindow(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
}
