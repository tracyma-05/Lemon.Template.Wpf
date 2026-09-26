using Lemon.Template.Avalonia.Infrastructures.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.Services.Updates;

/// <summary>
/// Reads the latest release (an update manifest, or a GitHub Release; see <see cref="UpdateOptions"/>),
/// compares it with the running version and picks the package for this platform.
/// </summary>
public sealed class UpdateService : IUpdateService, ISingletonDependency
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    // One client for the process lifetime: a new HttpClient per check would leak sockets.
    private static readonly Lazy<HttpClient> SharedClient = new(CreateClient);

    private readonly IConfiguration _configuration;
    private readonly ILocalizationService _localization;
    private readonly ILogger<UpdateService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _platform;

    public UpdateService(IConfiguration configuration, ILocalizationService localization, ILogger<UpdateService> logger)
        : this(configuration, localization, logger, SharedClient.Value, ResolveCurrentVersion())
    {
    }

    /// <summary>Test seam: a stubbed transport, a fixed "installed" version and, optionally, a platform.</summary>
    internal UpdateService(
        IConfiguration configuration,
        ILocalizationService localization,
        ILogger<UpdateService>? logger,
        HttpClient httpClient,
        Version currentVersion,
        string? platform = null)
    {
        _configuration = configuration;
        _localization = localization;
        _logger = logger ?? NullLogger<UpdateService>.Instance;
        _httpClient = httpClient;
        CurrentVersion = currentVersion;
        _platform = platform ?? UpdatePlatform.Current;
    }

    public Version CurrentVersion { get; }

    // Read on every call rather than cached: appsettings.json is loaded with reloadOnChange, so an edited
    // URL takes effect on the next check without a restart.
    private UpdateOptions Options =>
        _configuration.GetSection(UpdateOptions.SectionName).Get<UpdateOptions>() ?? new UpdateOptions();

    public bool IsEnabled => Options.ResolveManifestUri() is not null;

    public bool CheckOnStartup => IsEnabled && Options.CheckOnStartup;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var options = Options;
        var manifestUri = options.ResolveManifestUri();
        if (manifestUri is null)
        {
            return UpdateCheckResult.Failure(CurrentVersion, _localization.GetString("Update_Error_NotConfigured"));
        }

        var isGitHub = options.IsGitHub;
        string json;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
            if (isGitHub)
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update source {Url} returned HTTP {Status}.", manifestUri, (int)response.StatusCode);

                // GitHub answers 404 both before the first release and for a repository it will not show
                // anonymously (a private one); either way there is nothing this client can download.
                return UpdateCheckResult.Failure(
                    CurrentVersion,
                    isGitHub && response.StatusCode == HttpStatusCode.NotFound
                        ? _localization.GetString("Update_Error_NoRelease")
                        : _localization.Format("Update_Error_Http", (int)response.StatusCode));
            }

            json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // TaskCanceledException without our token being cancelled is HttpClient's timeout.
            _logger.LogWarning(ex, "Could not reach update source {Url}.", manifestUri);
            return UpdateCheckResult.Failure(CurrentVersion, _localization.GetString("Update_Error_Network"));
        }

        var manifest = isGitHub ? ParseGitHubRelease(json) : ParseManifest(json, manifestUri);
        if (manifest is null)
        {
            _logger.LogWarning("Update source {Url} is not in the expected format.", manifestUri);
            return UpdateCheckResult.Failure(CurrentVersion, _localization.GetString("Update_Error_Format"));
        }

        if (Normalize(manifest.Version) <= Normalize(CurrentVersion))
        {
            return new UpdateCheckResult(UpdateCheckStatus.UpToDate, CurrentVersion, manifest, null);
        }

        var downloadUrl = SelectDownload(manifest, UpdatePlatform.Candidates(_platform));
        if (downloadUrl is null)
        {
            _logger.LogWarning("Release {Version} has no package for {Platform}.", manifest.Version, _platform);
            return UpdateCheckResult.Failure(
                CurrentVersion,
                _localization.Format("Update_Error_NoPackage", manifest.Version.ToString(3), _platform));
        }

        return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, CurrentVersion, manifest, null, downloadUrl);
    }

    /// <summary>
    /// The package for the first matching platform candidate when the release lists packages per platform;
    /// the single <c>downloadUrl</c> when it does not; the release page when nothing else fits.
    /// </summary>
    internal static Uri? SelectDownload(UpdateManifest manifest, IReadOnlyList<string> candidates)
    {
        if (manifest.Downloads.Count > 0)
        {
            return UpdatePlatform.Select(manifest.Downloads, candidates) ?? manifest.PageUrl;
        }

        return manifest.DownloadUrl ?? manifest.PageUrl;
    }

    /// <summary>
    /// Parses the manifest JSON (see <see cref="UpdateManifest"/>); null when it is malformed or lacks a
    /// usable version or any link.
    /// </summary>
    internal static UpdateManifest? ParseManifest(string json, Uri manifestUri)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = document.RootElement;
            var version = TryParseVersion(GetString(root, "version"));
            var downloadUrl = ResolveLink(GetString(root, "downloadUrl"), manifestUri);
            var pageUrl = ResolveLink(GetString(root, "pageUrl"), manifestUri);

            var downloads = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
            if (TryGetProperty(root, "downloads", out var map) && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in map.EnumerateObject())
                {
                    var link = entry.Value.ValueKind == JsonValueKind.String
                        ? ResolveLink(entry.Value.GetString(), manifestUri)
                        : null;
                    if (link is not null && !string.IsNullOrWhiteSpace(entry.Name))
                    {
                        downloads[entry.Name.Trim().ToLowerInvariant()] = link;
                    }
                }
            }

            if (version is null || (downloadUrl is null && pageUrl is null && downloads.Count == 0))
            {
                return null;
            }

            return new UpdateManifest(version, downloadUrl, GetString(root, "releaseNotes"), ParseDate(GetString(root, "publishedAt")))
            {
                Downloads = downloads,
                PageUrl = pageUrl,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a GitHub "latest release" response: the tag is the version (<c>v1.2.0</c>), the body the release
    /// notes, and each package asset is filed under the platform its name mentions
    /// (<c>MyApp-1.2.0-osx-arm64.zip</c>, see <see cref="UpdatePlatform.FromFileName"/>).
    /// </summary>
    /// <returns>Null when the response is malformed or the tag is not a version.</returns>
    internal static UpdateManifest? ParseGitHubRelease(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var version = TryParseVersion(GetString(root, "tag_name")) ?? TryParseVersion(GetString(root, "name"));
            if (version is null)
            {
                return null;
            }

            var packages = new List<(string? Platform, int Rank, Uri Link)>();
            if (TryGetProperty(root, "assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = GetString(asset, "name");
                    var link = ResolveLink(GetString(asset, "browser_download_url"), null);
                    if (name is null || link is null || !UpdatePlatform.IsPackage(name))
                    {
                        continue;
                    }

                    packages.Add((UpdatePlatform.FromFileName(name), UpdatePlatform.PackageRank(name), link));
                }
            }

            var downloads = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in packages.Where(x => x.Platform is not null).GroupBy(x => x.Platform!))
            {
                downloads[group.Key] = group.OrderBy(x => x.Rank).First().Link;
            }

            // A release with a single package that names no platform: offer it to everyone.
            if (downloads.Count == 0 && packages.Count == 1)
            {
                downloads[UpdatePlatform.Any] = packages[0].Link;
            }

            return new UpdateManifest(version, null, GetString(root, "body"), ParseDate(GetString(root, "published_at")))
            {
                Downloads = downloads,
                PageUrl = ResolveLink(GetString(root, "html_url"), null),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Accepts <c>1.2</c>, <c>1.2.3</c>, <c>1.2.3.4</c>, an optional leading <c>v</c>, and ignores
    /// SemVer pre-release / build suffixes (<c>1.2.3-beta+abc</c> reads as 1.2.3).
    /// </summary>
    internal static Version? TryParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var suffix = value.IndexOfAny(['-', '+']);
        if (suffix >= 0)
        {
            value = value[..suffix];
        }

        return Version.TryParse(value, out var version) ? Normalize(version) : null;
    }

    /// <summary>
    /// <see cref="Version"/> treats missing components as -1, so 1.2 would sort below 1.2.0.
    /// Filling them with zero makes the comparison mean what people expect.
    /// </summary>
    internal static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    /// <summary>An http(s) link, resolved against <paramref name="baseUri"/> when relative and a base is given.</summary>
    private static Uri? ResolveLink(string? value, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Uri? uri;
        var parsed = baseUri is null
            ? Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri)
            : Uri.TryCreate(baseUri, value.Trim(), out uri);
        if (!parsed || uri is null)
        {
            return null;
        }

        // The link ends up in the default browser; anything but http(s) could launch arbitrary handlers.
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement root, string name) =>
        TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Version ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateService).Assembly;
        return Normalize(assembly.GetName().Version ?? new Version(1, 0, 0));
    }

    private static HttpClient CreateClient()
    {
        // GitHub's API refuses requests without a User-Agent, so every request names the app.
        var client = new HttpClient { Timeout = RequestTimeout };

        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateService).Assembly;
        var name = assembly.GetName();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
            new ProductHeaderValue(name.Name ?? "App", name.Version?.ToString(3) ?? "1.0.0")));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }
}
