namespace Lemon.Template.Avalonia.Services.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

/// <summary>Outcome of one update check.</summary>
/// <param name="Latest">The published release; null when the check failed.</param>
/// <param name="Error">Why the check failed, in words a user can act on; null otherwise.</param>
/// <param name="DownloadUrl">
/// What the download button opens: the package for this platform, or the release page when the release has
/// none for it. Set whenever <see cref="Status"/> is <see cref="UpdateCheckStatus.UpdateAvailable"/>.
/// </param>
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    UpdateManifest? Latest,
    string? Error,
    Uri? DownloadUrl = null)
{
    public static UpdateCheckResult Failure(Version current, string error) =>
        new(UpdateCheckStatus.Failed, current, null, error);
}
