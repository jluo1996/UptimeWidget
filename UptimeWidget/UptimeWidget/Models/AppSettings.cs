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

        /// <summary>Whether the widget position is locked, preventing drag-to-move.</summary>
        public bool PositionLocked { get; set; } = false;

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
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt or unreadable file: fall back to defaults.
            }

            return new AppSettings();
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
            catch (Exception)
            {
                // Best-effort save; ignore IO failures.
            }
        }
    }
}
