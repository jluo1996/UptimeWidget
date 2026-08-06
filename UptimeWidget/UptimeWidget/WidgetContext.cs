using Microsoft.Win32;
using System.Diagnostics;
using UptimeWidget.Items;
using UptimeWidget.Models;
using UptimeWidget.Update;

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
        private readonly UpdateService _updateService = new();

        public WidgetContext()
        {
            _settings = AppSettings.Load();

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

            ContextMenuStrip menu = new();
            ToolStripMenuItem settingsMenuItem = new("Settings…");
            settingsMenuItem.Click += OnOpenSettings;
            ToolStripMenuItem checkUpdatesMenuItem = new("Check for updates…");
            checkUpdatesMenuItem.Click += OnCheckForUpdates;
            ToolStripMenuItem aboutMenuItem = new("About…");
            aboutMenuItem.Click += OnAbout;
            ToolStripMenuItem exitMenuItem = new("Exit");
            exitMenuItem.Click += OnExit;

            _ = menu.Items.Add(settingsMenuItem);
            _ = menu.Items.Add(_showWidgetMenuItem);
            _ = menu.Items.Add(_lockPositionMenuItem);
            _ = menu.Items.Add(new ToolStripSeparator());
            _ = menu.Items.Add(checkUpdatesMenuItem);
            _ = menu.Items.Add(aboutMenuItem);
            _ = menu.Items.Add(exitMenuItem);

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

            // Fire-and-forget startup update check; never blocks or delays the widget.
            _ = CheckForUpdatesAsync(userInitiated: false);
        }

        private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            Shutdown();
        }

        /// <summary>Builds the runtime item for every persisted source.</summary>
        private List<IWidgetItem> BuildAvailableItems()
        {
            List<IWidgetItem> items = [];
            foreach (SourceInstance source in _settings.Sources)
            {
                SourceType? type = SourceTypeRegistry.Find(source.TypeId);
                if (type is null)
                {
                    continue;
                }

                try
                {
                    items.Add(type.Create(source));
                }
                catch (Exception ex)
                {
                    // Skip a source that fails to build rather than crash the widget.
                    Debug.WriteLine($"Failed to build source '{source.Id}' ({source.TypeId}): {ex}");
                }
            }

            return items;
        }

        /// <summary>Builds the widget's item labels from the enabled-items setting (in order).</summary>
        private void RebuildWidget()
        {
            List<IWidgetItem> available = BuildAvailableItems();
            List<IWidgetItem> enabled = _settings.EnabledItems
                .Select(id => available.FirstOrDefault(i => i.Id == id))
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
            // Snapshot so a Cancel can revert changes that were applied live.
            AppSettings snapshot = _settings.Clone();

            using SettingsForm dialog = new(_settings);
            dialog.SettingsApplied += OnSettingsApplied;
            DialogResult result = dialog.ShowDialog();
            dialog.SettingsApplied -= OnSettingsApplied;

            if (result == DialogResult.OK)
            {
                // OK already saved in the dialog; nothing more to persist.
                return;
            }

            // Cancel: restore the pre-dialog state, push it to the widget, and
            // persist so disk matches the reverted, in-memory settings.
            _settings.CopyFrom(snapshot);
            OnSettingsApplied(_settings);
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
                    FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                    version = $"{info.FileVersion}";
                }
            }
            catch
            {
                // Fall back to "unknown".
            }

            _ = MessageBox.Show(
                $"Uptime Widget\nVersion {version}",
                "About Uptime Widget",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private bool _updateCheckInProgress;

        private async void OnCheckForUpdates(object? sender, EventArgs e)
        {
            await CheckForUpdatesAsync(userInitiated: true);
        }

        /// <summary>
        /// Checks GitHub for a newer release. When <paramref name="userInitiated"/> is
        /// true, results (including "up to date" and errors) are surfaced via message
        /// boxes; when false (startup), only an available update prompts the user and
        /// everything else stays silent.
        /// </summary>
        private async Task CheckForUpdatesAsync(bool userInitiated)
        {
            if (_updateCheckInProgress)
            {
                return;
            }

            _updateCheckInProgress = true;
            try
            {
                UpdateCheckResult result = await _updateService.CheckForUpdatesAsync(
                    _settings.IncludePrereleaseUpdates);

                if (_isShuttingDown)
                {
                    return;
                }

                switch (result.Status)
                {
                    case UpdateStatus.UpdateAvailable:
                        PromptAndInstall(result);
                        break;

                    case UpdateStatus.UpToDate when userInitiated:
                        _ = MessageBox.Show(
                            $"You're running the latest version ({result.LatestVersion}).",
                            "Check for updates",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        break;

                    case UpdateStatus.Failed when userInitiated:
                        _ = MessageBox.Show(
                            $"Could not check for updates.\n\n{result.Error}",
                            "Check for updates",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        break;
                }
            }
            finally
            {
                _updateCheckInProgress = false;
            }
        }

        private async void PromptAndInstall(UpdateCheckResult result)
        {
            Version? current = UpdateService.GetCurrentVersion();
            string prereleaseNote = result.IsPrerelease ? " (prerelease)" : string.Empty;
            DialogResult choice = MessageBox.Show(
                $"A new version of UptimeWidget is available.\n\n" +
                $"Current version: {current}\n" +
                $"New version: {result.LatestVersion}{prereleaseNote}\n\n" +
                "Download and install it now?",
                "Update available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (choice != DialogResult.Yes)
            {
                return;
            }

            try
            {
                string installerPath = await _updateService.DownloadInstallerAsync(
                    result.DownloadUrl!, result.AssetName!);

                UpdateService.LaunchInstaller(installerPath);
                Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update download/launch failed: {ex}");
                _ = MessageBox.Show(
                    $"The update could not be downloaded or started.\n\n{ex.Message}",
                    "Update failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            Shutdown();
        }

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
