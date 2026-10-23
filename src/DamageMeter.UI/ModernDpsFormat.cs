using System;
using System.Globalization;

namespace DamageMeter.UI
{
    // Presentation only: retain integer DPS and use the same separators in every locale.
    public static class ModernDpsFormat
    {
        private static readonly NumberFormatInfo DisplayFormat = NumberFormatInfo.ReadOnly(
            new NumberFormatInfo { NumberGroupSeparator = ".", NumberDecimalDigits = 0 });

        public static string Format(long amount, long intervalTicks)
        {
            // Decimal multiplication avoids overflowing a long for large party totals.
            var dps = intervalTicks == 0 ? amount :
                decimal.Truncate(amount * (decimal)TimeSpan.TicksPerSecond / intervalTicks);
            return dps.ToString("N0", DisplayFormat);
        }
    }
}
