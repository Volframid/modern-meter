using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DamageMeter.UI.Controls;

/// <summary>A small vector icon rasterized at its current device size, independent of row position.</summary>
public sealed class PixelAlignedMetricIcon : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data),
        typeof(Geometry), typeof(PixelAlignedMetricIcon), new FrameworkPropertyMetadata(null, ArtworkChanged));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill),
        typeof(Brush), typeof(PixelAlignedMetricIcon), new FrameworkPropertyMetadata(Brushes.White, ArtworkChanged));
    public Geometry? Data { get => (Geometry?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    private BitmapSource? _bitmap;
    private Matrix _device;
    private bool _hasDevice;

    public PixelAlignedMetricIcon()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void ArtworkChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (PixelAlignedMetricIcon)sender;
        icon._bitmap = null;
        icon.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
        OnLayoutUpdated(this, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => LayoutUpdated -= OnLayoutUpdated;

    private Matrix DeviceTransform()
    {
        Visual root = this;
        while (VisualTreeHelper.GetParent(root) is Visual parent) root = parent;
        var transform = TransformToAncestor(root);
        var origin = transform.Transform(new Point());
        var x = transform.Transform(new Point(1, 0));
        var y = transform.Transform(new Point(0, 1));
        var matrix = new Matrix(x.X-origin.X, x.Y-origin.Y, y.X-origin.X, y.Y-origin.Y, origin.X, origin.Y);
        // Off-screen rendering has no presentation source; a real window supplies monitor DPI.
        if (PresentationSource.FromVisual(this)?.CompositionTarget is { } target)
            matrix.Append(target.TransformToDevice);
        return matrix;
    }

    private void OnLayoutUpdated(object? sender, EventArgs args)
    {
        var matrix = DeviceTransform();
        if (_hasDevice && matrix == _device) return;
        _device = matrix;
        _hasDevice = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (Data is null || Fill is null || RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        var bounds = Data.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
        var device = DeviceTransform();
        _device = device;
        _hasDevice = true;
        if (!device.HasInverse) return;
        // Meter surfaces only scale/translate; retain vector drawing if a future surface rotates.
        if (Math.Abs(device.M12) > .000001 || Math.Abs(device.M21) > .000001 || device.M11 <= 0 || device.M22 <= 0)
        {
            var fit = Math.Min(RenderSize.Width / bounds.Width, RenderSize.Height / bounds.Height);
            drawing.PushTransform(new MatrixTransform(new Matrix(fit,0,0,fit,-bounds.X*fit,-bounds.Y*fit)));
            drawing.DrawGeometry(Fill, null, Data);
            drawing.Pop();
            return;
        }

        var scale = Math.Min(RenderSize.Width * device.M11 / bounds.Width, RenderSize.Height * device.M22 / bounds.Height);
        var width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var fit = Math.Min(width / bounds.Width, height / bounds.Height);
                context.PushTransform(new MatrixTransform(new Matrix(fit,0,0,fit,
                    (width-bounds.Width*fit)/2-bounds.X*fit, (height-bounds.Height*fit)/2-bounds.Y*fit)));
                context.DrawGeometry(Fill, null, Data);
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            _bitmap = bitmap;
        }

        var left = Math.Round(device.OffsetX + (RenderSize.Width * device.M11-width)/2);
        var top = Math.Round(device.OffsetY + (RenderSize.Height * device.M22-height)/2);
        var inverse = device;
        inverse.Invert();
        drawing.PushTransform(new MatrixTransform(inverse));
        drawing.PushGuidelineSet(new GuidelineSet(new[] { left, left + width }, new[] { top, top + height }));
        drawing.DrawImage(_bitmap, new Rect(left, top, width, height));
        drawing.Pop();
        drawing.Pop();
    }
}
