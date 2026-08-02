using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace UptimeWidget.Items
{
    /// <summary>
    /// Shows how long a process (matched by its process name) has been running,
    /// based on its <see cref="Process.StartTime"/>. When several processes share
    /// the name, the earliest-started one that can be read is used.
    /// <para>
    /// Display precedence when no readable start time is found: if at least one
    /// matching process existed but access to its start time was denied (e.g. an
    /// elevated/system process while this app runs without administrator rights),
    /// a "requires admin" state is shown; otherwise a "not running" state is shown.
    /// </para>
    /// </summary>
    public sealed class ProcessUptimeItem : IWidgetItem
    {
        private readonly string _processName;
        private readonly string _label;

        // Cache of the resolved earliest-started matching process. A process's start
        // time never changes while it runs, and any process that appears later
        // necessarily started later, so the earliest can never change while the
        // cached process is still alive. This lets the common per-second refresh
        // avoid re-enumerating every process, and only forces a full rescan once the
        // cached process exits.
        private int? _cachedPid;
        private DateTime? _cachedStart;

        public ProcessUptimeItem(string id, string displayName, string processName)
        {
            Id = id;
            _processName = processName;
            _label = string.IsNullOrWhiteSpace(displayName) ? processName : displayName;
            Name = _label;
        }

        public string Id { get; }

        public string Name { get; }

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(1);

        public string GetDisplayText()
        {
            if (string.IsNullOrWhiteSpace(_processName))
            {
                return $"{_label}: no process selected";
            }

            // Fast path: reuse the cached earliest-started process if it is still alive.
            // StartTime is compared as well as the PID/name: Windows recycles PIDs, so a
            // restarted same-named process could reuse the old PID. A matching StartTime
            // pins the identity to the exact original instance and rejects any reuse.
            if (_cachedPid is int cachedPid && _cachedStart is DateTime cachedStart)
            {
                try
                {
                    using Process existing = Process.GetProcessById(cachedPid);
                    if (!existing.HasExited
                        && string.Equals(existing.ProcessName, _processName, StringComparison.OrdinalIgnoreCase)
                        && existing.StartTime == cachedStart)
                    {
                        return Format(cachedStart);
                    }
                }
                catch
                {
                    // Process is gone or unreadable: fall through to a full rescan.
                }

                _cachedPid = null;
                _cachedStart = null;
            }

            DateTime? earliestStart = null;
            int earliestPid = 0;
            bool sawAnyProcess = false;
            bool sawAccessDenied = false;
            Process[] processes = Process.GetProcessesByName(_processName);
            try
            {
                foreach (Process p in processes)
                {
                    sawAnyProcess = true;
                    try
                    {
                        DateTime start = p.StartTime;
                        if (earliestStart is null || start < earliestStart)
                        {
                            earliestStart = start;
                            earliestPid = p.Id;
                        }
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                    {
                        // Access denied (e.g. elevated/system process): running but
                        // unreadable without administrator rights.
                        sawAccessDenied = true;
                    }
                    catch (InvalidOperationException)
                    {
                        // Process has exited between enumeration and read, or has no
                        // meaningful start time (e.g. Idle/System): treat as not running.
                    }
                    catch (Win32Exception)
                    {
                        // Other native failure: unavailable, but not an admin issue.
                    }
                }
            }
            finally
            {
                foreach (Process p in processes)
                {
                    p.Dispose();
                }
            }

            if (earliestStart is null)
            {
                return sawAnyProcess && sawAccessDenied ? $"{_label}: requires admin" : $"{_label}: not running";
            }

            _cachedPid = earliestPid;
            _cachedStart = earliestStart;
            return Format(earliestStart.Value);
        }

        private string Format(DateTime start)
        {
            TimeSpan up = DateTime.Now - start;
            if (up < TimeSpan.Zero)
            {
                up = TimeSpan.Zero;
            }

            return $"{_label}: {UptimeFormatter.Format(up)}";
        }

        /// <summary>
        /// Returns the distinct names of currently running processes, sorted.
        /// <para>
        /// When this application is not running elevated, processes whose start time
        /// cannot be read without administrator rights are excluded, so the user is
        /// only offered processes whose uptime can actually be displayed. When running
        /// elevated, all running processes are returned.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> GetRunningProcessNames()
        {
            bool elevated = IsElevated();
            Process[] processes = Process.GetProcesses();
            try
            {
                IEnumerable<Process> readable = elevated
                    ? processes
                    : processes.Where(CanReadStartTime);

                return readable
                    .Select(p => p.ProcessName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            finally
            {
                foreach (Process p in processes)
                {
                    p.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns true if the current process is running with administrator rights.
        /// </summary>
        private static bool IsElevated()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessUptimeItem.IsElevated failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Returns true if this application can read the given process's start time
        /// (i.e. it does not require administrator rights that we lack).
        /// <para>
        /// This probes accessibility using native calls (<c>OpenProcess</c> with
        /// <c>PROCESS_QUERY_LIMITED_INFORMATION</c> followed by <c>GetProcessTimes</c>)
        /// instead of reading <see cref="Process.StartTime"/> in a try/catch. Reading
        /// <see cref="Process.StartTime"/> throws a <see cref="Win32Exception"/> for
        /// every inaccessible process, and throwing/catching once per denied process
        /// is slow enough to freeze the UI on systems with many processes. The native
        /// probe returns a failure code instead of throwing, so it is far cheaper while
        /// preserving the exact same filtering behaviour.
        /// </para>
        /// </summary>
        private static bool CanReadStartTime(Process p)
        {
            int pid;
            try
            {
                pid = p.Id;
            }
            catch
            {
                return false;
            }

            SafeProcessHandle handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle.IsInvalid)
            {
                return false;
            }

            try
            {
                return GetProcessTimes(handle, out _, out _, out _, out _);
            }
            finally
            {
                handle.Dispose();
            }
        }

        private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(
            SafeProcessHandle process,
            out long creationTime,
            out long exitTime,
            out long kernelTime,
            out long userTime);
    }
}
