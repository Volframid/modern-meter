using System;
using System.Windows;

namespace DamageMeter.UI;

public static class ModernNotificationPlacement
{
    // Always start with the user's anchor, never the previous transient card size/location.
    public static Point Place(Point anchor, Size size, Rect area)
    {
        if (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y) || area.IsEmpty ||
            !double.IsFinite(size.Width) || !double.IsFinite(size.Height)) return anchor;
        return new Point(Math.Clamp(anchor.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
            Math.Clamp(anchor.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    }
}
