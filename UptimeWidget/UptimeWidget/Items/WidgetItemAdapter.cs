namespace UptimeWidget.Items
{
    /// <summary>
    /// Wraps an existing <see cref="IWidgetItem"/> so it can be exposed under a
    /// caller-supplied <see cref="Id"/> and <see cref="Name"/>. This lets a
    /// user-created source instance carry its own identity and display label while
    /// delegating the actual metric behavior to the underlying item.
    /// </summary>
    public sealed class WidgetItemAdapter : IWidgetItem
    {
        private readonly IWidgetItem _inner;

        public WidgetItemAdapter(string id, string name, IWidgetItem inner)
        {
            Id = id;
            Name = name;
            _inner = inner;
        }

        public string Id { get; }

        public string Name { get; }

        public TimeSpan RefreshInterval => _inner.RefreshInterval;

        public string GetDisplayText()
        {
            return _inner.GetDisplayText();
        }
    }
}
