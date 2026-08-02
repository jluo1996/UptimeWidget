using UptimeWidget.Items;

namespace UptimeWidget.Models
{
    /// <summary>
    /// The kind of value a <see cref="ParameterDescriptor"/> accepts. Drives which
    /// control the editor dialog renders for the field.
    /// </summary>
    public enum ParameterKind
    {
        Text,
        Number,
        Bool,
        Choice,
        FilePath,
    }

    /// <summary>
    /// Declarative description of a single parameter belonging to a
    /// <see cref="SourceType"/>. The Add/Edit dialog renders and validates its form
    /// entirely from these descriptors, so new source types need no dialog code.
    /// </summary>
    public sealed class ParameterDescriptor
    {
        public ParameterDescriptor(
            string key,
            string label,
            ParameterKind kind,
            bool required = false,
            string? defaultValue = null,
            IReadOnlyList<string>? choices = null,
            string? placeholder = null,
            string? helpText = null,
            Func<string, string?>? validate = null,
            Func<IReadOnlyList<string>>? choicesProvider = null)
        {
            Key = key;
            Label = label;
            Kind = kind;
            Required = required;
            DefaultValue = defaultValue;
            Choices = choices;
            Placeholder = placeholder;
            HelpText = helpText;
            _validate = validate;
            ChoicesProvider = choicesProvider;
        }

        /// <summary>Stable key used to store the value in <see cref="SourceInstance.Parameters"/>.</summary>
        public string Key { get; }

        /// <summary>Human-readable label shown next to the field.</summary>
        public string Label { get; }

        public ParameterKind Kind { get; }

        public bool Required { get; }

        public string? DefaultValue { get; }

        /// <summary>Allowed values when <see cref="Kind"/> is <see cref="ParameterKind.Choice"/>.</summary>
        public IReadOnlyList<string>? Choices { get; }

        /// <summary>
        /// Optional supplier of live choices for a <see cref="ParameterKind.Choice"/>
        /// field, evaluated when the editor is shown (e.g. the current process list).
        /// Takes precedence over <see cref="Choices"/> when set.
        /// </summary>
        public Func<IReadOnlyList<string>>? ChoicesProvider { get; }

        public string? Placeholder { get; }

        public string? HelpText { get; }

        private readonly Func<string, string?>? _validate;

        /// <summary>
        /// Validates a raw string value. Returns null when valid, or an error message.
        /// Applies the required-field check first, then any custom validator.
        /// </summary>
        public string? Validate(string? value)
        {
            string trimmed = value?.Trim() ?? string.Empty;

            return Required && trimmed.Length == 0 ? $"{Label} is required." : trimmed.Length == 0 ? null : (_validate?.Invoke(trimmed));
        }
    }

    /// <summary>
    /// App-provided template describing a category of source the user can create
    /// (e.g. "System uptime"). Declares the parameter schema and a factory that
    /// turns a filled-in <see cref="SourceInstance"/> into a runtime
    /// <see cref="IWidgetItem"/>. The set of source types is fixed by the app.
    /// </summary>
    public sealed class SourceType
    {
        public SourceType(
            string id,
            string displayName,
            string description,
            IReadOnlyList<ParameterDescriptor> parameters,
            Func<SourceInstance, IWidgetItem> factory)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Parameters = parameters;
            _factory = factory;
        }

        /// <summary>Stable identifier persisted as <see cref="SourceInstance.TypeId"/>.</summary>
        public string Id { get; }

        /// <summary>Name shown in the type dropdown.</summary>
        public string DisplayName { get; }

        /// <summary>One-line explanation shown under the dropdown.</summary>
        public string Description { get; }

        /// <summary>Ordered parameter schema used to render and validate the form.</summary>
        public IReadOnlyList<ParameterDescriptor> Parameters { get; }

        private readonly Func<SourceInstance, IWidgetItem> _factory;

        /// <summary>Builds the runtime widget item for the given instance.</summary>
        public IWidgetItem Create(SourceInstance instance)
        {
            return _factory(instance);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
