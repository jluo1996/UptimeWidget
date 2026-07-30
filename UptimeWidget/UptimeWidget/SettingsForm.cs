using UptimeWidget.Items;
using UptimeWidget.Models;

namespace UptimeWidget
{
    /// <summary>
    /// Dialog for editing <see cref="AppSettings"/>. Changes are pushed live to the
    /// widget via the <see cref="SettingsApplied"/> event while the dialog is open,
    /// and saved when the user confirms.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly IReadOnlyList<IWidgetItem> _availableItems;

        private readonly CheckedListBox _itemsList;
        private readonly TrackBar _opacityBar;
        private readonly TrackBar _backgroundOpacityBar;
        private readonly NumericUpDown _intervalUpDown;
        private readonly NumericUpDown _fontSizeUpDown;
        private readonly CheckBox _alwaysOnTopCheck;
        private readonly CheckBox _startWithWindowsCheck;
        private readonly Button _foreColorButton;
        private readonly Button _backColorButton;

        private Color _foreColor;
        private Color _backColor;

        /// <summary>Raised whenever a setting changes, so the widget can update live.</summary>
        public event Action<AppSettings>? SettingsApplied;

        public SettingsForm(AppSettings settings, IReadOnlyList<IWidgetItem> availableItems)
        {
            _settings = settings;
            _availableItems = availableItems;
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
                Dock = DockStyle.Top,
                Padding = new Padding(12),
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            };
            _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            _ = layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

            // Items checklist.
            layout.Controls.Add(new Label { Text = "Items:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _itemsList = new CheckedListBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 100,
                Width = 200,
                CheckOnClick = true,
            };
            foreach (IWidgetItem item in _availableItems)
            {
                bool enabled = _settings.EnabledItems.Contains(item.Id);
                _ = _itemsList.Items.Add(new ItemEntry(item), enabled);
            }
            _itemsList.ItemCheck += (_, _) => BeginInvoke(ApplyLive);
            layout.Controls.Add(_itemsList, 1, 0);

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

            // OK / Cancel buttons.
            FlowLayoutPanel buttonPanel = new()
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                AutoSize = true,
            };
            Button okButton = new() { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(80, 0) };
            Button cancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(80, 0) };
            okButton.Click += (_, _) => { ApplyLive(); _settings.Save(); };
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(layout);
            Controls.Add(buttonPanel);
            Controls.SetChildIndex(buttonPanel, 0);
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
                    enabled.Add(entry.Item.Id);
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

            SettingsApplied?.Invoke(_settings);
        }

        private sealed class ItemEntry
        {
            public ItemEntry(IWidgetItem item)
            {
                Item = item;
            }

            public IWidgetItem Item { get; }

            public override string ToString()
            {
                return Item.Name;
            }
        }
    }
}
