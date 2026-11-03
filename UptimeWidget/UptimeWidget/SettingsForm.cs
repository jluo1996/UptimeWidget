using UptimeWidget.Models;

namespace UptimeWidget
{
    /// <summary>
    /// Dialog for editing <see cref="AppSettings"/>. Changes are pushed live to the
    /// widget via the <see cref="SettingsApplied"/> event while the dialog is open,
    /// and saved when the user confirms with OK. If the user cancels, the caller
    /// reverts the settings to their pre-dialog snapshot.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;

        private readonly CheckedListBox _itemsList;
        private readonly Button _editSourceButton;
        private readonly Button _removeSourceButton;
        private readonly Button _moveUpButton;
        private readonly Button _moveDownButton;

        private readonly ToolTip _itemsToolTip = new();

        // Tracks, for the most recent mouse-driven ItemCheck, whether the click
        // landed on the checkbox glyph. Used to suppress toggles from text clicks.
        private bool _pendingMouseCheck;
        private bool _pendingMouseCheckOnGlyph;
        private int _lastTooltipIndex = -1;

        private readonly TrackBar _opacityBar;
        private readonly TrackBar _backgroundOpacityBar;
        private readonly NumericUpDown _intervalUpDown;
        private readonly NumericUpDown _fontSizeUpDown;
        private readonly CheckBox _alwaysOnTopCheck;
        private readonly CheckBox _startWithWindowsCheck;
        private readonly CheckBox _hideNonRunningCheck;
        private readonly CheckBox _includePrereleaseCheck;
        private readonly CheckBox _checkForUpdatesCheck;
        private readonly Button _foreColorButton;
        private readonly Button _backColorButton;

        private Color _foreColor;
        private Color _backColor;

        /// <summary>Raised whenever a setting changes, so the widget can update live.</summary>
        public event Action<AppSettings>? SettingsApplied;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            _foreColor = Color.FromArgb(settings.ForeColorArgb);
            _backColor = Color.FromArgb(settings.BackColorArgb);

            Text = "Uptime Widget — Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(380, 0);

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            };
            _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

            // Items checklist with Add/Edit/Remove controls.
            layout.Controls.Add(new Label { Text = "Items:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _itemsList = new CheckedListBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 100,
                Width = 200,
                CheckOnClick = true,
            };
            _itemsList.MouseDown += ItemsList_MouseDown;
            _itemsList.ItemCheck += ItemsList_ItemCheck;
            _itemsList.SelectedIndexChanged += (_, _) => UpdateSourceButtons();
            _itemsList.MouseMove += ItemsList_MouseMove;
            _itemsList.MouseLeave += (_, _) => _lastTooltipIndex = -1;

            FlowLayoutPanel moveButtons = new()
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 0, 0),
            };
            TableLayoutPanel sourceButtons = new()
            {
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 0),
            };
            Button addSourceButton = new() { Text = "Add…", AutoSize = true };
            addSourceButton.Click += (_, _) => OnAddSource();
            _editSourceButton = new Button { Text = "Edit…", AutoSize = true };
            _editSourceButton.Click += (_, _) => OnEditSource();
            _removeSourceButton = new Button { Text = "Remove", AutoSize = true };
            _removeSourceButton.Click += (_, _) => OnRemoveSource();
            _moveUpButton = new Button { Text = "↑", AutoSize = true };
            _moveUpButton.Click += (_, _) => MoveSelected(-1);
            _moveDownButton = new Button { Text = "↓", AutoSize = true };
            _moveDownButton.Click += (_, _) => MoveSelected(1);
            moveButtons.Controls.Add(_moveUpButton);
            moveButtons.Controls.Add(_moveDownButton);
            sourceButtons.Controls.Add(addSourceButton, 0, 0);
            sourceButtons.Controls.Add(_editSourceButton, 1, 0);
            sourceButtons.Controls.Add(_removeSourceButton, 2, 0);

            TableLayoutPanel itemsContainer = new()
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0),
            };
            itemsContainer.Controls.Add(_itemsList, 0, 0);
            itemsContainer.Controls.Add(moveButtons, 0, 1);
            itemsContainer.Controls.Add(sourceButtons, 0, 2);
            layout.Controls.Add(itemsContainer, 1, 0);

            PopulateItemsList();
            UpdateSourceButtons();

            // Opacity.
            layout.Controls.Add(new Label { Text = "Opacity:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _opacityBar = new TrackBar
            {
                Minimum = 10,
                Maximum = 100,
                TickFrequency = 10,
                Value = (int)Math.Clamp(_settings.Opacity * 100, 10, 100),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Width = 200,
            };
            _opacityBar.ValueChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_opacityBar, 1, 1);

            // Background opacity (background color only; text stays opaque).
            layout.Controls.Add(new Label { Text = "Background opacity:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _backgroundOpacityBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                Value = (int)Math.Clamp(_settings.BackgroundOpacity * 100, 0, 100),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Width = 200,
            };
            _backgroundOpacityBar.ValueChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_backgroundOpacityBar, 1, 2);

            // Update interval.
            layout.Controls.Add(new Label { Text = "Update interval (ms):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            _intervalUpDown = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 60000,
                Increment = 1000,
                Value = Math.Clamp(_settings.UpdateIntervalMs, 100, 60000),
                Dock = DockStyle.Left,
                Width = 100,
            };
            _intervalUpDown.ValueChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_intervalUpDown, 1, 3);

            // Font size.
            layout.Controls.Add(new Label { Text = "Font size (pt):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            _fontSizeUpDown = new NumericUpDown
            {
                Minimum = 6,
                Maximum = 36,
                Increment = 1,
                DecimalPlaces = 0,
                Value = (decimal)Math.Clamp(_settings.FontSize, 6f, 36f),
                Dock = DockStyle.Left,
                Width = 100,
            };
            _fontSizeUpDown.ValueChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_fontSizeUpDown, 1, 4);

            const int COLOR_BUTTON_WIDTH = 48;
            const int COLOR_BUTTON_HEIGHT = 48;

            // Foreground color.
            layout.Controls.Add(new Label { Text = "Text color:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
            _foreColorButton = new Button { AutoSize = false, Size = new Size(COLOR_BUTTON_WIDTH, COLOR_BUTTON_HEIGHT), Anchor = AnchorStyles.Left, BackColor = _foreColor };
            _foreColorButton.Click += (_, _) => PickColor(ref _foreColor, _foreColorButton);
            layout.Controls.Add(_foreColorButton, 1, 5);

            // Background color.
            layout.Controls.Add(new Label { Text = "Background color:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
            _backColorButton = new Button { AutoSize = false, Size = new Size(COLOR_BUTTON_WIDTH, COLOR_BUTTON_HEIGHT), Anchor = AnchorStyles.Left, BackColor = _backColor };
            _backColorButton.Click += (_, _) => PickColor(ref _backColor, _backColorButton);
            layout.Controls.Add(_backColorButton, 1, 6);

            // Always on top.
            _alwaysOnTopCheck = new CheckBox
            {
                Text = "Always on top",
                Checked = _settings.AlwaysOnTop,
                AutoSize = true,
            };
            _alwaysOnTopCheck.CheckedChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_alwaysOnTopCheck, 0, 7);
            layout.SetColumnSpan(_alwaysOnTopCheck, 2);

            // Start with Windows.
            _startWithWindowsCheck = new CheckBox
            {
                Text = "Start with Windows",
                Checked = _settings.StartWithWindows,
                AutoSize = true,
            };
            _startWithWindowsCheck.CheckedChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_startWithWindowsCheck, 0, 8);
            layout.SetColumnSpan(_startWithWindowsCheck, 2);

            // Hide non-running processes.
            _hideNonRunningCheck = new CheckBox
            {
                Text = "Hide non-running processes",
                Checked = _settings.HideNonRunningProcesses,
                AutoSize = true,
            };
            _hideNonRunningCheck.CheckedChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_hideNonRunningCheck, 0, 9);
            layout.SetColumnSpan(_hideNonRunningCheck, 2);

            // Automatically check for updates on startup.
            _checkForUpdatesCheck = new CheckBox
            {
                Text = "Automatically check for updates",
                Checked = _settings.CheckForUpdatesOnStartup,
                AutoSize = true,
            };
            _checkForUpdatesCheck.CheckedChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_checkForUpdatesCheck, 0, 10);
            layout.SetColumnSpan(_checkForUpdatesCheck, 2);

            // Include prerelease updates.
            _includePrereleaseCheck = new CheckBox
            {
                Text = "Include prerelease updates",
                Checked = _settings.IncludePrereleaseUpdates,
                AutoSize = true,
            };
            _includePrereleaseCheck.CheckedChanged += (_, _) => ApplyLive();
            layout.Controls.Add(_includePrereleaseCheck, 0, 11);
            layout.SetColumnSpan(_includePrereleaseCheck, 2);

            // OK / Cancel buttons.
            FlowLayoutPanel buttonPanel = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            Button okButton = new() { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(80, 0) };
            Button cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(80, 0) };
            okButton.Click += (_, _) => { ApplyLive(); _settings.Save(); };
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            // Outer container so the form auto-sizes to fit both the settings grid and
            // the button row in each dimension.
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(layout, 0, 0);
            root.Controls.Add(buttonPanel, 0, 1);

            Controls.Add(root);
        }

        private void ItemsList_MouseDown(object? sender, MouseEventArgs e)
        {
            _pendingMouseCheck = false;
            _pendingMouseCheckOnGlyph = false;

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = _itemsList.IndexFromPoint(e.Location);
            if (index < 0 || index >= _itemsList.Items.Count)
            {
                return;
            }

            Rectangle itemRect = _itemsList.GetItemRectangle(index);
            Size glyphSize;
            using (Graphics g = _itemsList.CreateGraphics())
            {
                glyphSize = CheckBoxRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);
            }

            // The checkbox glyph is drawn at the left edge of the item, vertically
            // centered. Only allow the toggle when the click lands within it.
            int glyphTop = itemRect.Top + ((itemRect.Height - glyphSize.Height) / 2);
            Rectangle checkboxRect = new(itemRect.Left, glyphTop, glyphSize.Width, glyphSize.Height);
            _pendingMouseCheck = true;
            _pendingMouseCheckOnGlyph = checkboxRect.Contains(e.Location);
        }

        private void ItemsList_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            // Cancel toggles that originate from clicking the item's text rather
            // than the checkbox glyph. Keyboard toggles (no pending mouse click)
            // are always allowed.
            if (_pendingMouseCheck && !_pendingMouseCheckOnGlyph)
            {
                e.NewValue = e.CurrentValue;
                _pendingMouseCheck = false;
                return;
            }

            _pendingMouseCheck = false;
            if (IsHandleCreated)
            {
                _ = BeginInvoke(ApplyLive);
            }
        }

        private void ItemsList_MouseMove(object? sender, MouseEventArgs e)
        {
            int index = _itemsList.IndexFromPoint(e.Location);
            if (index == _lastTooltipIndex)
            {
                return;
            }

            _lastTooltipIndex = index;
            if (index >= 0 && index < _itemsList.Items.Count)
            {
                _itemsToolTip.SetToolTip(_itemsList, _itemsList.Items[index].ToString());
            }
            else
            {
                _itemsToolTip.SetToolTip(_itemsList, null);
            }
        }

        private void PickColor(ref Color target, Button button)
        {
            using ColorDialog dlg = new() { Color = target, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                target = dlg.Color;
                button.BackColor = target;
                ApplyLive();
            }
        }

        /// <summary>Writes the current control values into the settings and notifies listeners.</summary>
        private void ApplyLive()
        {
            List<string> enabled = [];
            foreach (object item in _itemsList.CheckedItems)
            {
                if (item is ItemEntry entry)
                {
                    enabled.Add(entry.Source.Id);
                }
            }
            _settings.EnabledItems = enabled;

            _settings.Opacity = _opacityBar.Value / 100.0;
            _settings.BackgroundOpacity = _backgroundOpacityBar.Value / 100.0;
            _settings.UpdateIntervalMs = (int)_intervalUpDown.Value;
            _settings.FontSize = (float)_fontSizeUpDown.Value;
            _settings.ForeColorArgb = _foreColor.ToArgb();
            _settings.BackColorArgb = _backColor.ToArgb();
            _settings.AlwaysOnTop = _alwaysOnTopCheck.Checked;
            _settings.StartWithWindows = _startWithWindowsCheck.Checked;
            _settings.HideNonRunningProcesses = _hideNonRunningCheck.Checked;
            _settings.IncludePrereleaseUpdates = _includePrereleaseCheck.Checked;
            _settings.CheckForUpdatesOnStartup = _checkForUpdatesCheck.Checked;

            SettingsApplied?.Invoke(_settings);
        }

        private sealed class ItemEntry
        {
            public ItemEntry(SourceInstance source)
            {
                Source = source;
            }

            public SourceInstance Source { get; }

            public bool IsSystemUptime => Source.TypeId == SourceTypeRegistry.UptimeTypeId;

            public override string ToString()
            {
                string typeName = SourceTypeRegistry.Find(Source.TypeId)?.DisplayName ?? Source.TypeId;
                return string.IsNullOrWhiteSpace(Source.DisplayName)
                    ? typeName
                    : $"{Source.DisplayName} ({typeName})";
            }
        }

        /// <summary>Fills the checklist from the persisted sources, preserving checked state.</summary>
        private void PopulateItemsList()
        {
            _itemsList.Items.Clear();
            foreach (SourceInstance source in _settings.Sources)
            {
                bool enabled = _settings.EnabledItems.Contains(source.Id);
                _ = _itemsList.Items.Add(new ItemEntry(source), enabled);
            }
        }

        private void UpdateSourceButtons()
        {
            // System uptime is a permanent built-in: it cannot be edited or removed.
            bool isEditable = _itemsList.SelectedItem is ItemEntry entry && !entry.IsSystemUptime;
            _editSourceButton.Enabled = isEditable;
            _removeSourceButton.Enabled = isEditable;
        }

        /// <summary>
        /// Moves the currently selected item by <paramref name="delta"/> positions,
        /// reordering both the backing <see cref="AppSettings.Sources"/> list and the
        /// checklist (preserving checked state), then applies the change live.
        /// </summary>
        private void MoveSelected(int delta)
        {
            int from = _itemsList.SelectedIndex;
            if (from < 0)
            {
                return;
            }

            int to = from + delta;
            MoveItem(from, to);
        }

        /// <summary>
        /// Reorders the source at <paramref name="from"/> to <paramref name="to"/> in both
        /// <see cref="AppSettings.Sources"/> and the checklist, preserving checked state and
        /// selection, then notifies listeners via <see cref="ApplyLive"/>.
        /// </summary>
        private void MoveItem(int from, int to)
        {
            if (from < 0 || from >= _settings.Sources.Count)
            {
                return;
            }

            to = Math.Clamp(to, 0, _settings.Sources.Count - 1);
            if (from == to)
            {
                return;
            }

            SourceInstance moved = _settings.Sources[from];
            _settings.Sources.RemoveAt(from);
            _settings.Sources.Insert(to, moved);

            PopulateItemsList();
            _itemsList.SelectedIndex = to;
            UpdateSourceButtons();
            ApplyLive();
        }

        private void OnAddSource()
        {
            using SourceEditorForm dialog = new(null);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _settings.Sources.Add(dialog.Result);
            _settings.EnabledItems.Add(dialog.Result.Id);
            PopulateItemsList();
            UpdateSourceButtons();
            ApplyLive();
        }

        private void OnEditSource()
        {
            if (_itemsList.SelectedItem is not ItemEntry entry || entry.IsSystemUptime)
            {
                return;
            }

            using SourceEditorForm dialog = new(entry.Source);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            int index = _settings.Sources.FindIndex(s => s.Id == entry.Source.Id);
            if (index >= 0)
            {
                _settings.Sources[index] = dialog.Result;
            }
            PopulateItemsList();
            UpdateSourceButtons();
            ApplyLive();
        }

        private void OnRemoveSource()
        {
            if (_itemsList.SelectedItem is not ItemEntry entry || entry.IsSystemUptime)
            {
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                $"Remove \"{entry.Source.DisplayName}\"?",
                "Remove source",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            _ = _settings.Sources.RemoveAll(s => s.Id == entry.Source.Id);
            _ = _settings.EnabledItems.RemoveAll(id => id == entry.Source.Id);
            PopulateItemsList();
            UpdateSourceButtons();
            ApplyLive();
        }
    }
}
