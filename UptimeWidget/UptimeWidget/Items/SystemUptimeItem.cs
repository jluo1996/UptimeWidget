namespace UptimeWidget.Items
{
    /// <summary>
    /// Shows how long the system has been running since last boot.
    /// </summary>
    public sealed class SystemUptimeItem : IWidgetItem
    {
        public string Id => "uptime";

        public string Name => "System uptime";

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(1);

        public bool IsRunning => true;

        public string GetDisplayText()
        {
            TimeSpan up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return $"System: {UptimeFormatter.Format(up)}";
        }
    }
}
