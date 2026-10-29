using System;
using System.Windows;
using System.Windows.Media;

namespace DamageMeter.UI.Broadcast;

/// <summary>Shares window content without making the native Window a cyclic-brush root.</summary>
public sealed class BroadcastContentProjection : IDisposable
{
    private readonly VisualBrush _brush = new() { AutoLayoutContent = false, Stretch = Stretch.Fill,
        ViewboxUnits = BrushMappingMode.Absolute };
    private FrameworkElement? _content;
    private Rect _contentBox, _windowBox;
    private Matrix _matrix;
    private Brush? _background;
    public DrawingVisual Visual { get; } = new();

    public DrawingVisual Update(Window window, FrameworkElement content)
    {
        var contentBox = new Rect(content.RenderSize);
        var windowBox = new Rect(window.RenderSize);
        var toWindow = content.TransformToAncestor(window);
        var origin = toWindow.Transform(new Point());
        var x = toWindow.Transform(new Point(1, 0)); var y = toWindow.Transform(new Point(0, 1));
        var matrix = new Matrix(x.X - origin.X, x.Y - origin.Y, y.X - origin.X, y.Y - origin.Y, origin.X, origin.Y);
        if (ReferenceEquals(_content, content) && _contentBox == contentBox && _windowBox == windowBox &&
            _matrix == matrix && ReferenceEquals(_background, window.Background)) return Visual;
        _content = content; _contentBox = contentBox; _windowBox = windowBox; _matrix = matrix; _background = window.Background;
        // Set AutoLayoutContent before assigning any visual. This projection
        // reads layout owned by the interactive window and never arranges it.
        _brush.Visual = content;
        var outer = VisualTreeHelper.GetTransform(content)?.Value ?? Matrix.Identity;
        var box = new MatrixTransform(outer).TransformBounds(contentBox); var offset = VisualTreeHelper.GetOffset(content);
        box.Offset(offset.X, offset.Y); _brush.Viewbox = box;
        Visual.Clip = new RectangleGeometry(windowBox);
        using (var draw = Visual.RenderOpen())
        {
            if (_background != null) draw.DrawRectangle(_background, null, windowBox);
            draw.PushTransform(new MatrixTransform(matrix));
            draw.DrawRectangle(_brush, null, contentBox);
            draw.Pop();
        }
        return Visual;
    }

    public void Dispose() { _brush.Visual = null; _content = null; using var draw = Visual.RenderOpen(); }
}
