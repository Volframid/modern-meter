using System;
using System.Windows;
using System.Windows.Media;

namespace DamageMeter.UI;

public static class ModernWindowPlacement
{
    public static Point Center(Rect physicalWorkArea, Size logicalWindowSize, Matrix fromDevice)
    {
        var area = Rect.Transform(physicalWorkArea, fromDevice);
        return new Point(area.Left + Math.Max(0, area.Width - logicalWindowSize.Width) / 2,
            area.Top + Math.Max(0, area.Height - logicalWindowSize.Height) / 2);
    }
}
