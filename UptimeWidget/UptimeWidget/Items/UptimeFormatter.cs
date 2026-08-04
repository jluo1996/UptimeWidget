using System.Globalization;

namespace UptimeWidget.Items
{
    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a fixed-width uptime string in the
    /// form <c>DD:HH:MM:SS</c>. Every component is always present and
    /// zero-padded so the rendered text keeps a constant character count as
    /// time progresses, preventing the widget from resizing on each tick.
    /// </summary>
    internal static class UptimeFormatter
    {
        public static string Format(TimeSpan up)
        {
            if (up < TimeSpan.Zero)
            {
                up = TimeSpan.Zero;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}:{3:00}",
                up.Days,
                up.Hours,
                up.Minutes,
                up.Seconds);
        }
    }
}
