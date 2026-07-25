using Microsoft.Win32;

namespace UptimeWidget
{
    /// <summary>
    /// Manages the "Start with Windows" auto-run entry under the current user's
    /// registry Run key.
    /// </summary>
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "UptimeWidget";

        /// <summary>Adds or removes the Run entry to match <paramref name="enabled"/>.</summary>
        public static void SetStartWithWindows(bool enabled)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key is null)
                {
                    return;
                }

                if (enabled)
                {
                    key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
                }
                else if (key.GetValue(ValueName) is not null)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // Best-effort; ignore registry access failures.
            }
        }

        /// <summary>Returns true if the Run entry currently exists.</summary>
        public static bool IsEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }
}
