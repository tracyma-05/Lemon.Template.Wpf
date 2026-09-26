namespace Lemon.Template.Avalonia.Services.Updates;

/// <summary>
/// The latest published release, read from the update manifest or from a GitHub Release.
/// </summary>
/// <remarks>
/// Manifest format (property names are matched case-insensitively; links may be relative to the manifest):
/// <code>
/// {
///   "version": "1.2.0",
///   "downloads": {
///     "win-x64":   "https://example.com/downloads/MyApp-1.2.0-win-x64.zip",
///     "osx-arm64": "https://example.com/downloads/MyApp-1.2.0-osx-arm64.zip",
///     "osx-x64":   "https://example.com/downloads/MyApp-1.2.0-osx-x64.zip"
///   },
///   "downloadUrl": "https://example.com/downloads/MyApp-1.2.0-win-x64.zip",
///   "pageUrl": "https://example.com/myapp",
///   "releaseNotes": "Fixed …",
///   "publishedAt": "2026-09-26T08:00:00Z"
/// }
/// </code>
/// <c>version</c> is required, plus at least one of <c>downloads</c>, <c>downloadUrl</c> or <c>pageUrl</c>.
/// When <c>downloads</c> is present the package is chosen from it by platform (see <see cref="UpdatePlatform"/>)
/// and <c>downloadUrl</c> is ignored: it is there for older clients that only know a single link. A
/// computer without a matching package is sent to <c>pageUrl</c>.
/// </remarks>
public sealed record UpdateManifest(
    Version Version,
    Uri? DownloadUrl,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt)
{
    /// <summary>Package per platform key (<c>win-x64</c>, <c>osx-arm64</c>, <c>any</c>, …).</summary>
    public IReadOnlyDictionary<string, Uri> Downloads { get; init; } = new Dictionary<string, Uri>();

    /// <summary>A web page for the release, used when no package suits this computer.</summary>
    public Uri? PageUrl { get; init; }
}
