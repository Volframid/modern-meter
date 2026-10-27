using System.Windows;
using System.Windows.Media;

namespace DamageMeter.UI;

/// <summary>Original Modern Meter identity: a bright impact cutting through fractured violet plates.</summary>
public static class ModernDamageMark
{
    public static DrawingImage Glyph { get; } = Create(false);
    public static DrawingImage ApplicationImage { get; } = Create(true);

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static LinearGradientBrush Gradient(string start, string end) => new(
        (Color)ColorConverter.ConvertFromString(start), (Color)ColorConverter.ConvertFromString(end),
        new Point(0, 0), new Point(1, 1));

    private static DrawingImage Create(bool tile)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Transparent bounds keep the same view box for the splash, ICO and SVG.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 64, 64));
            if (tile)
                dc.DrawRoundedRectangle(Gradient("#302740", "#15141E"),
                    new Pen(Brush("#615078"), 1.2), new Rect(2, 2, 60, 60), 14, 14);
            dc.DrawGeometry(Gradient("#C5A2FF", "#7750B9"), null,
                Geometry.Parse("M10,24 L24,10 L34,12 L26,25 L28,31 L18,42 L11,37 Z"));
            dc.DrawGeometry(Gradient("#8C5ADC", "#AD8AFF"), null,
                Geometry.Parse("M36,23 L48,18 L54,30 L49,45 L36,55 L27,51 L36,39 L33,32 Z"));
            dc.DrawGeometry(Brush("#F7F2FF"), null,
                Geometry.Parse("M46,8 L37,26 L44,23 L20,54 L27,36 L20,39 Z"));
            var spark = new Pen(Brush("#D9C4FF"), 2.6)
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(spark, new Point(12, 10), new Point(17, 16));
            dc.DrawLine(spark, new Point(48, 51), new Point(53, 56));
        }
        group.Freeze();
        var image = new DrawingImage(group); image.Freeze();
        return image;
    }
}
