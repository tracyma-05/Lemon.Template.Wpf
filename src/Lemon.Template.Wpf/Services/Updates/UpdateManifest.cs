namespace Lemon.Template.Wpf.Services.Updates;

/// <summary>
/// What the update URL returns: the latest published release.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "version": "1.2.0",
///   "downloadUrl": "https://example.com/downloads/MyApp-1.2.0.zip",
///   "releaseNotes": "Fixed …",
///   "publishedAt": "2026-09-26T08:00:00Z"
/// }
/// </code>
/// Property names are matched case-insensitively. <c>downloadUrl</c> may be relative to the manifest
/// address; only <c>version</c> and <c>downloadUrl</c> are required.
/// </remarks>
public sealed record UpdateManifest(
    Version Version,
    Uri DownloadUrl,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt);
