using UptimeWidget.Items;

namespace UptimeWidget.Models
{
    /// <summary>
    /// Fixed catalog of source types the app supports. The Add/Edit dialog and the
    /// settings checklist are driven entirely by this registry, so introducing a new
    /// source type is a matter of adding a single <see cref="SourceType"/> entry.
    /// </summary>
    public static class SourceTypeRegistry
    {
        /// <summary>Id of the built-in system-uptime source type.</summary>
        public const string UptimeTypeId = "uptime";

        /// <summary>Id of the built-in process-uptime source type.</summary>
        public const string ProcessTypeId = "process";

        /// <summary>All registered source types, in display order.</summary>
        public static IReadOnlyList<SourceType> Types { get; } = [
            new SourceType(
                id: UptimeTypeId,
                displayName: "System uptime",
                description: "Shows how long this computer has been running since last boot.",
                parameters: [],
                factory: CreateUptimeItem),
            new SourceType(
                id: ProcessTypeId,
                displayName: "Process uptime",
                description: "Shows how long a selected process has been running. To monitor a process that requires admin rights, run this app as administrator.",
                parameters:
                [
                    new ParameterDescriptor(
                        key: "processName",
                        label: "Process",
                        kind: ParameterKind.Choice,
                        required: true,
                        choicesProvider: ProcessUptimeItem.GetRunningProcessNames),
                ],
                factory: CreateProcessItem),
        ];

        /// <summary>Finds a source type by id, or null if it is not registered.</summary>
        public static SourceType? Find(string? typeId)
        {
            return typeId is null ? null : Types.FirstOrDefault(t => t.Id == typeId);
        }

        private static IWidgetItem CreateUptimeItem(SourceInstance instance)
        {
            string name = string.IsNullOrWhiteSpace(instance.DisplayName)
                ? "System uptime"
                : instance.DisplayName;
            return new WidgetItemAdapter(instance.Id, name, new SystemUptimeItem());
        }

        private static IWidgetItem CreateProcessItem(SourceInstance instance)
        {
            _ = instance.Parameters.TryGetValue("processName", out string? processName);
            return new ProcessUptimeItem(
                instance.Id,
                instance.DisplayName,
                processName ?? string.Empty);
        }
    }
}
