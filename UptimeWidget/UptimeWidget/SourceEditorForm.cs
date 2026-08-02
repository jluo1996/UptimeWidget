using System.Diagnostics;
using UptimeWidget.Items;
using UptimeWidget.Models;

namespace UptimeWidget
{
    /// <summary>
    /// Schema-driven dialog for creating or editing a <see cref="SourceInstance"/>.
    /// The user picks a <see cref="SourceType"/> from a dropdown; the parameter
    /// fields are generated dynamically from that type's
    /// <see cref="ParameterDescriptor"/> list. A Test button builds a throwaway
    /// runtime item and previews its output.
    /// </summary>
    internal sealed class SourceEditorForm : Form
    {
        private readonly bool _isNew;

        private readonly ComboBox _typeCombo;
        private readonly Label _typeDescription;
        private readonly TextBox _displayNameBox;
        private readonly TableLayoutPanel _paramPanel;
        private readonly Label _testResult;

        // Editors for the currently selected type's parameters, keyed by parameter key.
        private readonly Dictionary<string, Control> _paramEditors = [];

        /// <summary>The resulting instance after the dialog is accepted with OK.</summary>
        public SourceInstance Result { get; }

        public SourceEditorForm(SourceInstance? existing)
        {
            _isNew = existing is null;
            Result = existing is null
                ? new SourceInstance()
                : new SourceInstance
                {
                    Id = existing.Id,
                    TypeId = existing.TypeId,
                    DisplayName = existing.DisplayName,
                    Parameters = new Dictionary<string, string>(existing.Parameters),
                };

            Text = _isNew ? "Add source" : "Edit source";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(360, 0);

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            };
            _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

            // Type selector.
            layout.Controls.Add(new Label { Text = "Type:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _typeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Width = 200,
            };
            foreach (SourceType type in SourceTypeRegistry.Types)
            {
                // System uptime is a permanent built-in and cannot be added as a
                // new source, so hide it from the Add dialog's type list.
                if (_isNew && type.Id == SourceTypeRegistry.UptimeTypeId)
                {
                    continue;
                }
                _ = _typeCombo.Items.Add(type);
            }
            _typeCombo.SelectedIndexChanged += (_, _) => OnTypeChanged();
            layout.Controls.Add(_typeCombo, 1, 0);

            // Type description spanning both columns.
            _typeDescription = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 0, 3, 6),
                MaximumSize = new Size(300, 0),
            };
            layout.Controls.Add(_typeDescription, 1, 1);

            // Display name.
            layout.Controls.Add(new Label { Text = "Display name:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _displayNameBox = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Width = 200,
                Text = Result.DisplayName,
            };
            layout.Controls.Add(_displayNameBox, 1, 2);

            // Dynamic parameter panel spanning both columns.
            _paramPanel = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 6, 0, 0),
            };
            _ = _paramPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            _ = _paramPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            layout.Controls.Add(_paramPanel, 0, 3);
            layout.SetColumnSpan(_paramPanel, 2);

            // Test row.
            Button testButton = new() { Text = "Test", AutoSize = true, Anchor = AnchorStyles.Left };
            testButton.Click += (_, _) => OnTest();
            layout.Controls.Add(testButton, 0, 4);

            _testResult = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                MaximumSize = new Size(300, 0),
                Margin = new Padding(3, 6, 3, 0),
            };
            layout.Controls.Add(_testResult, 1, 4);

            // OK / Cancel.
            FlowLayoutPanel buttons = new()
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            Button cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            Button okButton = new() { Text = "OK", AutoSize = true };
            okButton.Click += OnOk;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);

            // Root container stacks the content and buttons vertically so the
            // AutoSize form reliably grows to fit all generated fields.
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            };
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(layout, 0, 0);
            root.Controls.Add(buttons, 0, 1);

            Controls.Add(root);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            // Select the current (or first) type, which populates the param fields.
            int index = 0;
            for (int i = 0; i < SourceTypeRegistry.Types.Count; i++)
            {
                if (SourceTypeRegistry.Types[i].Id == Result.TypeId)
                {
                    index = i;
                    break;
                }
            }
            if (_typeCombo.Items.Count > 0)
            {
                _typeCombo.SelectedIndex = index;
            }
        }

        private SourceType? SelectedType => _typeCombo.SelectedItem as SourceType;

        private void OnTypeChanged()
        {
            SourceType? type = SelectedType;
            _typeDescription.Text = type?.Description ?? string.Empty;
            _testResult.Text = string.Empty;
            RebuildParamFields(type);

            // Suggest a display name for new sources when the field is empty.
            if (_isNew && string.IsNullOrWhiteSpace(_displayNameBox.Text) && type is not null)
            {
                _displayNameBox.Text = type.DisplayName;
            }
        }

        private void RebuildParamFields(SourceType? type)
        {
            _paramPanel.SuspendLayout();
            _paramPanel.Controls.Clear();
            _paramEditors.Clear();

            if (type is not null)
            {
                foreach (ParameterDescriptor p in type.Parameters)
                {
                    Label label = new() { Text = p.Label + ":", AutoSize = true, Anchor = AnchorStyles.Left };
                    Control editor = CreateEditor(p);
                    _paramEditors[p.Key] = editor;
                    _paramPanel.Controls.Add(label);
                    _paramPanel.Controls.Add(editor);
                }
            }

            _paramPanel.ResumeLayout();
        }

        private Control CreateEditor(ParameterDescriptor p)
        {
            _ = Result.Parameters.TryGetValue(p.Key, out string? current);
            string value = current ?? p.DefaultValue ?? string.Empty;

            switch (p.Kind)
            {
                case ParameterKind.Bool:
                    return new CheckBox
                    {
                        Checked = bool.TryParse(value, out bool b) && b,
                        Anchor = AnchorStyles.Left,
                        AutoSize = true,
                    };

                case ParameterKind.Choice:
                    ComboBox combo = new()
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        Width = 180,
                    };
                    IReadOnlyList<string>? choices = p.ChoicesProvider is not null
                        ? SafeGetChoices(p.ChoicesProvider)
                        : p.Choices;
                    if (choices is not null)
                    {
                        foreach (string choice in choices)
                        {
                            _ = combo.Items.Add(choice);
                        }
                    }
                    // Preserve a previously stored value even if it is not currently live.
                    if (value.Length > 0 && !combo.Items.Contains(value))
                    {
                        _ = combo.Items.Add(value);
                    }
                    combo.SelectedItem = value;
                    if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                    {
                        combo.SelectedIndex = 0;
                    }
                    return combo;

                case ParameterKind.FilePath:
                    TableLayoutPanel row = new()
                    {
                        ColumnCount = 2,
                        AutoSize = true,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        Margin = new Padding(0),
                    };
                    _ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    _ = row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    TextBox pathBox = new()
                    {
                        Text = value,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        Width = 150,
                        PlaceholderText = p.Placeholder ?? string.Empty,
                    };
                    Button browse = new() { Text = "…", AutoSize = true };
                    browse.Click += (_, _) =>
                    {
                        using OpenFileDialog ofd = new();
                        if (ofd.ShowDialog(this) == DialogResult.OK)
                        {
                            pathBox.Text = ofd.FileName;
                        }
                    };
                    row.Controls.Add(pathBox, 0, 0);
                    row.Controls.Add(browse, 1, 0);
                    // Tag the container with the text box so we can read it back.
                    row.Tag = pathBox;
                    return row;

                default:
                    return new TextBox
                    {
                        Text = value,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right,
                        Width = 180,
                        PlaceholderText = p.Placeholder ?? string.Empty,
                    };
            }
        }

        private static IReadOnlyList<string> SafeGetChoices(Func<IReadOnlyList<string>> provider)
        {
            try
            {
                return provider();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Choice provider failed: {ex}");
                return [];
            }
        }

        private static string ReadEditorValue(Control editor)
        {
            return editor switch
            {
                CheckBox cb => cb.Checked.ToString(),
                ComboBox combo => combo.SelectedItem?.ToString() ?? string.Empty,
                TableLayoutPanel row when row.Tag is TextBox tb => tb.Text.Trim(),
                TextBox tb => tb.Text.Trim(),
                _ => string.Empty,
            };
        }

        /// <summary>Collects the current field values into the instance, or returns an error.</summary>
        private string? CollectInto(SourceInstance target)
        {
            SourceType? type = SelectedType;
            if (type is null)
            {
                return "Please choose a source type.";
            }

            string displayName = _displayNameBox.Text.Trim();
            if (displayName.Length == 0)
            {
                return "Display name is required.";
            }

            Dictionary<string, string> values = [];
            foreach (ParameterDescriptor p in type.Parameters)
            {
                string raw = _paramEditors.TryGetValue(p.Key, out Control? editor)
                    ? ReadEditorValue(editor)
                    : string.Empty;

                string? error = p.Validate(raw);
                if (error is not null)
                {
                    return error;
                }

                values[p.Key] = raw;
            }

            target.TypeId = type.Id;
            target.DisplayName = displayName;
            target.Parameters = values;
            return null;
        }

        private void OnTest()
        {
            SourceInstance probe = new();
            string? error = CollectInto(probe);
            if (error is not null)
            {
                _testResult.ForeColor = Color.Firebrick;
                _testResult.Text = error;
                return;
            }

            SourceType? type = SourceTypeRegistry.Find(probe.TypeId);
            if (type is null)
            {
                _testResult.ForeColor = Color.Firebrick;
                _testResult.Text = "Unknown source type.";
                return;
            }

            try
            {
                IWidgetItem item = type.Create(probe);
                string output = item.GetDisplayText();
                _testResult.ForeColor = SystemColors.ControlText;
                _testResult.Text = $"Preview: {output}";
            }
            catch (Exception ex)
            {
                _testResult.ForeColor = Color.Firebrick;
                _testResult.Text = $"Failed: {ex.Message}";
            }
        }

        private void OnOk(object? sender, EventArgs e)
        {
            string? error = CollectInto(Result);
            if (error is not null)
            {
                _ = MessageBox.Show(this, error, "Invalid source",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
