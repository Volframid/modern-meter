using System.Windows;
using System.Windows.Media;

namespace DamageMeter.UI;

/// <summary>The selected MM monogram, drawn natively without a tile or background.</summary>
public static class ModernDamageMark
{
    public static DrawingImage Glyph { get; } = Create();
    public static DrawingImage ApplicationImage => Glyph;

    private static DrawingImage Create()
    {
        var violet = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#AD8AFB"),
            (Color)ColorConverter.ConvertFromString("#9F7AF1"), new Point(0, 0), new Point(1, 1));
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Fixed transparent view box keeps the same optical alignment in
            // window captions, the splash and every native ICO resolution.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 64, 64));
            dc.PushTransform(new MatrixTransform(60d / 859, 0, 0, 60d / 859,
                2 - 196d * 60 / 859, 32 - (378d + 483d / 2) * 60 / 859));
            dc.DrawGeometry(violet, null, Geometry.Parse(
                "M196,777 L484,378 L621,570 L557,658 L484,525 L303,777 Z"));
            dc.DrawGeometry(violet, null, Geometry.Parse(
                "M398,613 L455,563 L532,709 L772,378 L890,546 L827,636 L772,525 L532,861 Z"));
            dc.DrawGeometry(violet, null, Geometry.Parse(
                "M686,696 L744,616 L824,732 L969,464 L1055,464 L829,848 Z"));
            dc.DrawGeometry(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F3F8")), null,
                Geometry.Parse("M969,464 L1055,464 L982,571 L911,571 Z"));
            dc.Pop();
        }
        group.Freeze();
        var image = new DrawingImage(group); image.Freeze();
        return image;
    }
}
