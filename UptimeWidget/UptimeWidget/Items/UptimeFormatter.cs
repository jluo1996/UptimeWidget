using System.Text;

namespace UptimeWidget.Items
{
    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as an uptime string, omitting any
    /// day/hour/minute/second component whose value is 0.
    /// </summary>
    internal static class UptimeFormatter
    {
        public static string Format(TimeSpan up)
        {
            if (up < TimeSpan.Zero)
            {
                up = TimeSpan.Zero;
            }

            StringBuilder sb = new();
            AppendUnit(sb, up.Days, "d");
            AppendUnit(sb, up.Hours, "h");
            AppendUnit(sb, up.Minutes, "m");
            AppendUnit(sb, up.Seconds, "s");

            return sb.Length == 0 ? "0s" : sb.ToString();
        }

        private static void AppendUnit(StringBuilder sb, int value, string unit)
        {
            if (value == 0)
            {
                return;
            }

            if (sb.Length > 0)
            {
                _ = sb.Append(' ');
            }

            _ = sb.Append(value).Append(unit);
        }
    }
}
