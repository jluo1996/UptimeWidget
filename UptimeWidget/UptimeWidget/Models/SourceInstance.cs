namespace UptimeWidget.Models
{
    /// <summary>
    /// A user-created source: a reference to a <see cref="SourceType"/> plus the
    /// parameter values the user supplied. Persisted in settings.json and turned
    /// into a runtime <see cref="Items.IWidgetItem"/> via the type's factory.
    /// </summary>
    public sealed class SourceInstance
    {
        /// <summary>
        /// Stable unique id. Doubles as the runtime item id, so it plugs directly
        /// into <see cref="AppSettings.EnabledItems"/> ordering/enable logic.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("n");

        /// <summary>Which <see cref="SourceType"/> this instance is built from.</summary>
        public string TypeId { get; set; } = string.Empty;

        /// <summary>User-facing label shown for this source (checklist and widget).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Filled-in parameter values, keyed by <see cref="ParameterDescriptor.Key"/>.</summary>
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
