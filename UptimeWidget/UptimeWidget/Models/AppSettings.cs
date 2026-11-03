using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UptimeWidget.Models
{
    /// <summary>
    /// User-configurable settings, persisted as JSON in %AppData%\UptimeWidget\settings.json.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Item ids that are enabled, in display order (top to bottom).</summary>
        public List<string> EnabledItems { get; set; } = ["uptime"];

        /// <summary>
        /// User-created sources, each an instance of a registered source type.
        /// Persisted here and reloaded on every launch. <see cref="EnabledItems"/>
        /// references these by <see cref="SourceInstance.Id"/>.
        /// </summary>
        public List<SourceInstance> Sources { get; set; } = [];

        /// <summary>Persisted widget position. Null means auto-position (bottom-right).</summary>
        public int? PositionX { get; set; }
        public int? PositionY { get; set; }

        /// <summary>Whole-window opacity (background and text), 0.1 - 1.0.</summary>
        public double Opacity { get; set; } = 0.85;

        /// <summary>Opacity of the background color only; text stays fully opaque. 0.1 - 1.0.</summary>
        public double BackgroundOpacity { get; set; } = 0.85;

        public string FontFamily { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 24f;

        /// <summary>ARGB color values.</summary>
        public int ForeColorArgb { get; set; } = unchecked((int)0xFFFFFFFF);
        public int BackColorArgb { get; set; } = unchecked((int)0xFF1E1E1E);

        /// <summary>Global refresh tick interval in milliseconds.</summary>
        public int UpdateIntervalMs { get; set; } = 1000;

        public bool AlwaysOnTop { get; set; } = true;

        /// <summary>
        /// Whether the widget window is currently shown. Runtime-only state; never
        /// persisted so the widget always shows on launch.
        /// </summary>
        [JsonIgnore]
        public bool WidgetVisible { get; set; } = true;

        public bool StartWithWindows { get; set; } = false;

        /// <summary>
        /// When true, update checks also consider prerelease (nightly) GitHub
        /// releases; otherwise only stable releases are offered.
        /// </summary>
        public bool IncludePrereleaseUpdates { get; set; } = false;

        /// <summary>
        /// When true, the app automatically checks for updates on startup;
        /// otherwise the startup check is skipped. Manual update checks are
        /// unaffected by this setting.
        /// </summary>
        public bool CheckForUpdatesOnStartup { get; set; } = false;

        /// <summary>Whether the widget position is locked, preventing drag-to-move.</summary>
        public bool PositionLocked { get; set; } = false;

        /// <summary>
        /// When true, items that are not currently running (e.g. a monitored
        /// process showing "not running") are hidden from the widget entirely.
        /// </summary>
        public bool HideNonRunningProcesses { get; set; } = false;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UptimeWidget");

        public static string SettingsPath =>
            Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>
        /// Loads settings from disk. Returns defaults if the file is missing or corrupt.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded is not null)
                    {
                        loaded.NormalizeSources();
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                // Corrupt or unreadable file: fall back to defaults.
                Debug.WriteLine($"AppSettings.Load failed: {ex}");
            }

            AppSettings defaults = new();
            defaults.NormalizeSources();
            return defaults;
        }

        /// <summary>
        /// Ensures <see cref="Sources"/> is consistent with the source-type registry
        /// and with <see cref="EnabledItems"/>:
        /// <list type="bullet">
        /// <item>Always ensures the permanent built-in system-uptime source exists,
        /// seeding it (enabled by default) when missing.</item>
        /// <item>Drops sources whose type is no longer registered.</item>
        /// <item>Removes EnabledItems entries that reference missing sources.</item>
        /// </list>
        /// </summary>
        private void NormalizeSources()
        {
            // Drop sources with an unknown type first, so the built-in check below
            // reflects only valid, registered sources.
            Sources = Sources
                .Where(s => SourceTypeRegistry.Find(s.TypeId) is not null)
                .ToList();

            // System uptime is a permanent built-in: it must always exist and can
            // never be added or removed by the user. Ensure exactly one uptime
            // source is present, seeding it at the front when missing.
            if (!Sources.Any(s => s.TypeId == SourceTypeRegistry.UptimeTypeId))
            {
                SourceInstance uptime = new()
                {
                    TypeId = SourceTypeRegistry.UptimeTypeId,
                    DisplayName = "System uptime",
                };
                Sources.Insert(0, uptime);

                // Enable it by default on fresh installs, and honor legacy settings
                // that referenced the built-in via the "uptime" enabled-item id.
                bool wasEnabled = EnabledItems.Count == 0
                    || EnabledItems.Contains(SourceTypeRegistry.UptimeTypeId);
                if (wasEnabled)
                {
                    EnabledItems.Insert(0, uptime.Id);
                }
            }

            // Keep only enabled ids that still reference an existing source.
            HashSet<string> valid = Sources.Select(s => s.Id).ToHashSet();
            EnabledItems = EnabledItems.Where(valid.Contains).ToList();
        }

        /// <summary>
        /// Creates a deep copy of these settings. Used to snapshot state before the
        /// settings dialog opens so it can be restored if the user cancels.
        /// </summary>
        public AppSettings Clone()
        {
            AppSettings clone = new();
            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// Overwrites every field of this instance with the values from
        /// <paramref name="other"/>, deep-copying the mutable collections. Reverts
        /// in place so existing references to this instance observe the change.
        /// </summary>
        public void CopyFrom(AppSettings other)
        {
            EnabledItems = [.. other.EnabledItems];
            Sources = other.Sources
                .Select(s => new SourceInstance
                {
                    Id = s.Id,
                    TypeId = s.TypeId,
                    DisplayName = s.DisplayName,
                    Parameters = new Dictionary<string, string>(s.Parameters),
                })
                .ToList();
            PositionX = other.PositionX;
            PositionY = other.PositionY;
            Opacity = other.Opacity;
            BackgroundOpacity = other.BackgroundOpacity;
            FontFamily = other.FontFamily;
            FontSize = other.FontSize;
            ForeColorArgb = other.ForeColorArgb;
            BackColorArgb = other.BackColorArgb;
            UpdateIntervalMs = other.UpdateIntervalMs;
            AlwaysOnTop = other.AlwaysOnTop;
            WidgetVisible = other.WidgetVisible;
            StartWithWindows = other.StartWithWindows;
            IncludePrereleaseUpdates = other.IncludePrereleaseUpdates;
            CheckForUpdatesOnStartup = other.CheckForUpdatesOnStartup;
            PositionLocked = other.PositionLocked;
            HideNonRunningProcesses = other.HideNonRunningProcesses;
        }

        /// <summary>
        /// Persists settings to disk, creating the directory if needed.
        /// </summary>
        public void Save()
        {
            try
            {
                _ = Directory.CreateDirectory(SettingsDirectory);
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                // Best-effort save; ignore IO failures.
                Debug.WriteLine($"AppSettings.Save failed: {ex}");
            }
        }
    }
}
