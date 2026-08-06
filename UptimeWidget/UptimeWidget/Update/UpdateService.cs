using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace UptimeWidget.Update
{
    /// <summary>Outcome of an update check.</summary>
    internal enum UpdateStatus
    {
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    /// <summary>Result of <see cref="UpdateService.CheckForUpdatesAsync"/>.</summary>
    internal sealed record UpdateCheckResult(
        UpdateStatus Status,
        Version? LatestVersion = null,
        string? DownloadUrl = null,
        string? AssetName = null,
        string? Error = null,
        bool IsPrerelease = false)
    {
        public static UpdateCheckResult UpToDate(Version latest) =>
            new(UpdateStatus.UpToDate, latest);

        public static UpdateCheckResult Available(
            Version latest, string url, string assetName, bool isPrerelease = false) =>
            new(UpdateStatus.UpdateAvailable, latest, url, assetName, IsPrerelease: isPrerelease);

        public static UpdateCheckResult Fail(string error) =>
            new(UpdateStatus.Failed, Error: error);
    }

    /// <summary>
    /// Checks GitHub Releases for a newer version of UptimeWidget, downloads the
    /// bootstrapper installer, and hands off to it. The existing WiX bundle/MSI
    /// perform the actual in-place upgrade (stable UpgradeCodes, per-user MSI).
    /// </summary>
    internal sealed class UpdateService
    {
        private static readonly string Repo = ResolveRepo();

        private static readonly string LatestReleaseUrl =
            $"https://api.github.com/repos/{Repo}/releases/latest";

        private static readonly string AllReleasesUrl =
            $"https://api.github.com/repos/{Repo}/releases?per_page=30";

        private const string InstallerAssetSuffix = "-Setup.exe";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            HttpClient client = new();
            // GitHub's API requires a User-Agent.
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("UptimeWidget", GetCurrentVersion()?.ToString() ?? "0"));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        /// <summary>
        /// Determines the GitHub "owner/repo" used for update checks. Resolution order:
        /// an optional runtime environment variable, then the value baked in at build
        /// time via the <c>UpdateRepo</c> assembly metadata (defaulted in the .csproj).
        /// </summary>
        private static string ResolveRepo()
        {
            string? fromEnv = Environment.GetEnvironmentVariable("UPTIMEWIDGET_UPDATE_REPO");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return fromEnv.Trim();
            }

            string? fromMetadata = typeof(UpdateService).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "UpdateRepo")?.Value;

            if (string.IsNullOrWhiteSpace(fromMetadata))
            {
                throw new InvalidOperationException(
                    "No update repository configured. Set the UPTIMEWIDGET_UPDATE_REPO " +
                    "environment variable or build with -p:UpdateRepo=owner/name.");
            }

            return fromMetadata.Trim();
        }

        /// <summary>
        /// Queries GitHub for the newest applicable release and compares it to the
        /// running version. When <paramref name="includePrereleases"/> is false, only
        /// the latest stable release is considered; when true, prerelease (nightly)
        /// releases are also included and the highest version wins. Never throws;
        /// failures are returned as <see cref="UpdateStatus.Failed"/>.
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(
            bool includePrereleases = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Version? current = GetCurrentVersion();
                if (current is null)
                {
                    return UpdateCheckResult.Fail("Could not determine the current version.");
                }

                return includePrereleases
                    ? await CheckIncludingPrereleasesAsync(current, cancellationToken)
                        .ConfigureAwait(false)
                    : await CheckLatestStableAsync(current, cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService.CheckForUpdatesAsync failed: {ex}");
                return UpdateCheckResult.Fail(ex.Message);
            }
        }

        /// <summary>Checks the latest stable (non-prerelease) release only.</summary>
        private async Task<UpdateCheckResult> CheckLatestStableAsync(
            Version current,
            CancellationToken cancellationToken)
        {
            using HttpResponseMessage response =
                await Http.GetAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            await using Stream stream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument doc =
                await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out JsonElement tagElement))
            {
                return UpdateCheckResult.Fail("Release response had no tag_name.");
            }

            Version? latest = ParseVersionFromTag(tagElement.GetString());
            if (latest is null)
            {
                return UpdateCheckResult.Fail(
                    $"Could not parse version from tag '{tagElement.GetString()}'.");
            }

            if (latest <= current)
            {
                return UpdateCheckResult.UpToDate(latest);
            }

            (string? url, string? assetName) = SelectInstallerAsset(root);
            if (url is null || assetName is null)
            {
                return UpdateCheckResult.Fail(
                    "Newer release found but no installer asset was available.");
            }

            return UpdateCheckResult.Available(latest, url, assetName, isPrerelease: false);
        }

        /// <summary>
        /// Checks all recent releases (including prereleases) and picks the highest
        /// version that has an installer asset. Drafts are ignored.
        /// </summary>
        private async Task<UpdateCheckResult> CheckIncludingPrereleasesAsync(
            Version current,
            CancellationToken cancellationToken)
        {
            using HttpResponseMessage response =
                await Http.GetAsync(AllReleasesUrl, cancellationToken).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            await using Stream stream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument doc =
                await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
            {
                return UpdateCheckResult.Fail("Unexpected releases response.");
            }

            Version? bestVersion = null;
            string? bestUrl = null;
            string? bestAsset = null;
            bool bestIsPrerelease = false;
            Version? highestSeen = null;

            foreach (JsonElement release in root.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out JsonElement draft)
                    && draft.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                if (!release.TryGetProperty("tag_name", out JsonElement tagElement))
                {
                    continue;
                }

                Version? version = ParseVersionFromTag(tagElement.GetString());
                if (version is null)
                {
                    continue;
                }

                if (highestSeen is null || version > highestSeen)
                {
                    highestSeen = version;
                }

                (string? url, string? assetName) = SelectInstallerAsset(release);
                if (url is null || assetName is null)
                {
                    continue;
                }

                if (bestVersion is null || version > bestVersion)
                {
                    bestVersion = version;
                    bestUrl = url;
                    bestAsset = assetName;
                    bestIsPrerelease =
                        release.TryGetProperty("prerelease", out JsonElement prerelease)
                        && prerelease.ValueKind == JsonValueKind.True;
                }
            }

            if (bestVersion is null)
            {
                return highestSeen is not null && highestSeen > current
                    ? UpdateCheckResult.Fail(
                        "Newer release found but no installer asset was available.")
                    : UpdateCheckResult.UpToDate(highestSeen ?? current);
            }

            return bestVersion > current
                ? UpdateCheckResult.Available(bestVersion, bestUrl!, bestAsset!, bestIsPrerelease)
                : UpdateCheckResult.UpToDate(bestVersion);
        }

        /// <summary>
        /// Downloads the installer asset to a temp folder and returns its path.
        /// </summary>
        public async Task<string> DownloadInstallerAsync(
            string url,
            string assetName,
            CancellationToken cancellationToken = default)
        {
            string dir = Path.Combine(Path.GetTempPath(), "UptimeWidget");
            _ = Directory.CreateDirectory(dir);
            string destination = Path.Combine(dir, assetName);

            using HttpResponseMessage response = await Http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            await using (Stream source =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (FileStream file = new(
                destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            return destination;
        }

        /// <summary>
        /// Starts the downloaded bootstrapper (progress-bar UI). The caller should
        /// request app exit so the installer can replace running files; the bundle's
        /// LaunchTarget relaunches the app after a successful upgrade.
        /// </summary>
        public static void LaunchInstaller(string installerPath)
        {
            ProcessStartInfo psi = new()
            {
                FileName = installerPath,
                Arguments = "/passive /norestart",
                UseShellExecute = true,
            };
            _ = Process.Start(psi);
        }

        /// <summary>Reads the running executable's file version.</summary>
        public static Version? GetCurrentVersion()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    return null;
                }

                FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
                return string.IsNullOrEmpty(info.FileVersion)
                    ? null
                    : NormalizeVersion(info.FileVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService.GetCurrentVersion failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Parses a release tag such as "v1.2.3" or "1.2.3-nightly" into a
        /// <see cref="Version"/>. Returns null when no version can be extracted.
        /// </summary>
        public static Version? ParseVersionFromTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            string trimmed = tag.Trim();
            if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            {
                trimmed = trimmed[1..];
            }

            // Drop any suffix such as "-nightly" or build metadata.
            int dash = trimmed.IndexOf('-');
            if (dash >= 0)
            {
                trimmed = trimmed[..dash];
            }

            return NormalizeVersion(trimmed);
        }

        /// <summary>
        /// Normalizes a dotted version string to a 4-part <see cref="Version"/> for
        /// stable comparison (e.g. "1.2" -> 1.2.0.0). Returns null when unparsable.
        /// </summary>
        public static Version? NormalizeVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Version.TryParse(value, out Version? parsed))
            {
                return null;
            }

            return new Version(
                parsed.Major,
                parsed.Minor,
                parsed.Build < 0 ? 0 : parsed.Build,
                parsed.Revision < 0 ? 0 : parsed.Revision);
        }

        /// <summary>
        /// Chooses the release asset whose name ends with the installer suffix.
        /// </summary>
        public static (string? Url, string? Name) SelectInstallerAsset(JsonElement release)
        {
            if (!release.TryGetProperty("assets", out JsonElement assets)
                || assets.ValueKind != JsonValueKind.Array)
            {
                return (null, null);
            }

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out JsonElement nameElement))
                {
                    continue;
                }

                string? name = nameElement.GetString();
                if (name is null
                    || !name.EndsWith(InstallerAssetSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                    && urlElement.GetString() is string url)
                {
                    return (url, name);
                }
            }

            return (null, null);
        }
    }
}
