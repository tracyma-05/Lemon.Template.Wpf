namespace Lemon.Template.Wpf.Services.Updates;

/// <summary>
/// The <c>Update</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The check-for-updates button only exists when <see cref="Enabled"/> is true <em>and</em>
/// <see cref="Url"/> is an absolute http(s) address, so a project that never publishes updates
/// shows no dead button.
/// </remarks>
public sealed class UpdateOptions
{
    public const string SectionName = "Update";

    public bool Enabled { get; set; }

    /// <summary>Address of the update manifest (JSON, see <see cref="UpdateManifest"/>).</summary>
    public string? Url { get; set; }

    /// <summary>Check quietly once the main window is shown, and prompt only when something newer exists.</summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>The manifest address when the feature is switched on and usable; otherwise null.</summary>
    public Uri? ResolveManifestUri()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(Url))
        {
            return null;
        }

        return Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }
}
