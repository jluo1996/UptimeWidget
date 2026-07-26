using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using UptimeWidget.Items;
using UptimeWidget.Models;

namespace UptimeWidget
{
    /// <summary>
    /// Application context that owns the tray icon, its context menu, the floating
    /// widget window, and the list of available metric providers.
    /// </summary>
    internal sealed class WidgetContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ToolStripMenuItem _showWidgetMenuItem;
        private readonly ToolStripMenuItem _lockPositionMenuItem;
        private readonly WidgetForm _widget;
        private readonly AppSettings _settings;
        private readonly List<IWidgetItem> _availableItems;

        public WidgetContext()
        {
            _settings = AppSettings.Load();

            _availableItems = new List<IWidgetItem>
            {
                new UptimeItem(),
            };

            _widget = new WidgetForm();
            _widget.LocationPersisted += OnWidgetMoved;
            _widget.PositionLocked = _settings.PositionLocked;

            _showWidgetMenuItem = new ToolStripMenuItem("Show widget")
            {
                CheckOnClick = true,
                Checked = _settings.WidgetVisible,
            };
            _showWidgetMenuItem.Click += OnToggleShowWidget;

            _lockPositionMenuItem = new ToolStripMenuItem("Lock position")
            {
                CheckOnClick = true,
                Checked = _settings.PositionLocked,
            };
            _lockPositionMenuItem.Click += OnToggleLockPosition;

            var menu = new ContextMenuStrip();
            var settingsMenuItem = new ToolStripMenuItem("Settings…");
            settingsMenuItem.Click += OnOpenSettings;
            var aboutMenuItem = new ToolStripMenuItem("About…");
            aboutMenuItem.Click += OnAbout;
            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += OnExit;

            menu.Items.Add(settingsMenuItem);
            menu.Items.Add(_showWidgetMenuItem);
            menu.Items.Add(_lockPositionMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(aboutMenuItem);
            menu.Items.Add(exitMenuItem);

            _trayIcon = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "Uptime Widget",
                Visible = true,
                ContextMenuStrip = menu,
            };
            _trayIcon.DoubleClick += OnToggleShowWidget;

            RebuildWidget();
            _widget.StartRefresh(_settings);
            ApplyVisibility(_settings.WidgetVisible);
            StartupManager.SetStartWithWindows(_settings.StartWithWindows);

            // Exit cleanly when Windows or the installer's Restart Manager asks the
            // app to close (e.g. during uninstall), so the process fully terminates
            // and releases the single-instance mutex.
            SystemEvents.SessionEnding += OnSessionEnding;
        }

        private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            Shutdown();
        }

        /// <summary>Builds the widget's item labels from the enabled-items setting (in order).</summary>
        private void RebuildWidget()
        {
            List<IWidgetItem> enabled = _settings.EnabledItems
                .Select(id => _availableItems.FirstOrDefault(i => i.Id == id))
                .Where(i => i is not null)
                .Cast<IWidgetItem>()
                .ToList();

            _widget.BuildItems(enabled, _settings);
            _widget.ApplyPosition(_settings);
        }

        private void OnWidgetMoved(Point location)
        {
            _settings.PositionX = location.X;
            _settings.PositionY = location.Y;
            _settings.Save();
        }

        private void OnToggleShowWidget(object? sender, EventArgs e)
        {
            bool visible = !_settings.WidgetVisible;
            ApplyVisibility(visible);
            _settings.WidgetVisible = visible;
            _settings.Save();
        }

        private void OnToggleLockPosition(object? sender, EventArgs e)
        {
            bool locked = !_settings.PositionLocked;
            _settings.PositionLocked = locked;
            _widget.PositionLocked = locked;
            _lockPositionMenuItem.Checked = locked;
            _settings.Save();
        }

        private void ApplyVisibility(bool visible)
        {
            _showWidgetMenuItem.Checked = visible;
            if (visible)
            {
                _widget.Show();
                _widget.ApplyPosition(_settings);
            }
            else
            {
                _widget.Hide();
            }
        }

        private void OnOpenSettings(object? sender, EventArgs e)
        {
            using var dialog = new SettingsForm(_settings, _availableItems);
            dialog.SettingsApplied += OnSettingsApplied;
            dialog.ShowDialog();
            dialog.SettingsApplied -= OnSettingsApplied;
            _settings.Save();
        }

        private void OnSettingsApplied(AppSettings settings)
        {
            RebuildWidget();
            _widget.ApplyAppearance(settings);
            _widget.StartRefresh(settings);
            StartupManager.SetStartWithWindows(settings.StartWithWindows);
        }

        private void OnAbout(object? sender, EventArgs e)
        {
            string version = "unknown";
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                    version = $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
                }
            }
            catch
            {
                // Fall back to "unknown".
            }

            MessageBox.Show(
                $"Uptime Widget\nVersion {version}",
                "About Uptime Widget",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnExit(object? sender, EventArgs e) => Shutdown();

        private bool _isShuttingDown;

        private void Shutdown()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _widget.StopRefresh();
            _widget.Dispose();
            ExitThread();
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "app.ico");
                if (File.Exists(path))
                {
                    return new Icon(path);
                }
            }
            catch
            {
                // Fall through to system default.
            }

            return SystemIcons.Application;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trayIcon?.Dispose();
                _widget?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
