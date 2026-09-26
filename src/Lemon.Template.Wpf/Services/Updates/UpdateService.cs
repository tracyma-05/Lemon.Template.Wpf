using Lemon.Template.Wpf.Infrastructures.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Services.Updates;

/// <summary>
/// Reads the update manifest named by <c>Update:Url</c> and compares it with the running version.
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

    public UpdateService(IConfiguration configuration, ILocalizationService localization, ILogger<UpdateService> logger)
        : this(configuration, localization, logger, SharedClient.Value, ResolveCurrentVersion())
    {
    }

    /// <summary>Test seam: a stubbed transport and a fixed "installed" version.</summary>
    internal UpdateService(
        IConfiguration configuration,
        ILocalizationService localization,
        ILogger<UpdateService>? logger,
        HttpClient httpClient,
        Version currentVersion)
    {
        _configuration = configuration;
        _localization = localization;
        _logger = logger ?? NullLogger<UpdateService>.Instance;
        _httpClient = httpClient;
        CurrentVersion = currentVersion;
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
        var manifestUri = Options.ResolveManifestUri();
        if (manifestUri is null)
        {
            return UpdateCheckResult.Failure(CurrentVersion, _localization.GetString("Update_Error_NotConfigured"));
        }

        string json;
        try
        {
            using var response = await _httpClient.GetAsync(manifestUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update manifest {Url} returned HTTP {Status}.", manifestUri, (int)response.StatusCode);
                return UpdateCheckResult.Failure(
                    CurrentVersion,
                    _localization.Format("Update_Error_Http", (int)response.StatusCode));
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
            _logger.LogWarning(ex, "Could not reach update manifest {Url}.", manifestUri);
            return UpdateCheckResult.Failure(CurrentVersion, _localization.GetString("Update_Error_Network"));
        }

        var manifest = ParseManifest(json, manifestUri);
        if (manifest is null)
        {
            _logger.LogWarning("Update manifest {Url} is not in the expected format.", manifestUri);
            return UpdateCheckResult.Failure(CurrentVersion, _localization.GetString("Update_Error_Format"));
        }

        var status = Normalize(manifest.Version) > Normalize(CurrentVersion)
            ? UpdateCheckStatus.UpdateAvailable
            : UpdateCheckStatus.UpToDate;

        return new UpdateCheckResult(status, CurrentVersion, manifest, null);
    }

    /// <summary>
    /// Parses the manifest JSON; null when it is malformed or lacks a usable version or download link.
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
            var downloadUrl = ResolveDownloadUrl(GetString(root, "downloadUrl"), manifestUri);
            if (version is null || downloadUrl is null)
            {
                return null;
            }

            DateTimeOffset? publishedAt = DateTimeOffset.TryParse(
                GetString(root, "publishedAt"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : null;

            return new UpdateManifest(version, downloadUrl, GetString(root, "releaseNotes"), publishedAt);
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

    private static Uri? ResolveDownloadUrl(string? value, Uri manifestUri)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(manifestUri, value.Trim(), out var uri))
        {
            return null;
        }

        // The link ends up in the default browser; anything but http(s) could launch arbitrary handlers.
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    private static string? GetString(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }

        return null;
    }

    private static Version ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateService).Assembly;
        return Normalize(assembly.GetName().Version ?? new Version(1, 0, 0));
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };

        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateService).Assembly;
        var name = assembly.GetName();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
            new ProductHeaderValue(name.Name ?? "App", name.Version?.ToString(3) ?? "1.0.0")));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }
}
