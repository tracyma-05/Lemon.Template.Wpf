namespace Lemon.Template.Wpf.Services.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

/// <summary>Outcome of one update check.</summary>
/// <param name="Latest">The published release; null when the check failed.</param>
/// <param name="Error">Why the check failed, in words a user can act on; null otherwise.</param>
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    UpdateManifest? Latest,
    string? Error)
{
    public static UpdateCheckResult Failure(Version current, string error) =>
        new(UpdateCheckStatus.Failed, current, null, error);
}
