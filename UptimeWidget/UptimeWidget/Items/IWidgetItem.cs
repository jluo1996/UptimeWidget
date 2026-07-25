namespace UptimeWidget.Items
{
    /// <summary>
    /// A single metric shown as one line in the widget. New metrics (CPU, RAM, disk, net)
    /// can be added by implementing this interface.
    /// </summary>
    public interface IWidgetItem
    {
        /// <summary>Stable identifier used for settings (enabled state, ordering).</summary>
        string Id { get; }

        /// <summary>Human-readable name shown in the settings checklist.</summary>
        string Name { get; }

        /// <summary>Current text to render on the widget.</summary>
        string GetDisplayText();

        /// <summary>How often this item's display text should be refreshed.</summary>
        TimeSpan RefreshInterval { get; }
    }
}
