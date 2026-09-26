using System.Text.RegularExpressions;

namespace Lemon.Template.Avalonia.Services.Updates;

/// <summary>
/// The <c>Update</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// Two sources are supported:
/// <list type="bullet">
/// <item><c>"Provider": "Manifest"</c> (the default): <see cref="Url"/> names a JSON manifest, see
/// <see cref="UpdateManifest"/>. Next.Hub serves one at <c>…/api/app/desktop-apps/{key}/latest</c>.</item>
/// <item><c>"Provider": "GitHub"</c>: <see cref="GitHubRepository"/> names <c>owner/repo</c>, and the latest
/// published GitHub Release is used; its assets are matched to the platform by file name.</item>
/// </list>
/// The check-for-updates button only exists when <see cref="Enabled"/> is true <em>and</em> the chosen
/// source is usable, so a project that never publishes updates shows no dead button.
/// </remarks>
public sealed partial class UpdateOptions
{
    public const string SectionName = "Update";
    public const string ManifestProvider = "Manifest";
    public const string GitHubProvider = "GitHub";

    public bool Enabled { get; set; }

    /// <summary><c>Manifest</c> (default when empty) or <c>GitHub</c>; anything else turns the feature off.</summary>
    public string? Provider { get; set; }

    /// <summary>Address of the update manifest (JSON, see <see cref="UpdateManifest"/>).</summary>
    public string? Url { get; set; }

    /// <summary><c>owner/repo</c>, or the repository's https://github.com/owner/repo address.</summary>
    public string? GitHubRepository { get; set; }

    /// <summary>Check quietly once the main window is shown, and prompt only when something newer exists.</summary>
    public bool CheckOnStartup { get; set; } = true;

    public bool IsGitHub => string.Equals(Provider?.Trim(), GitHubProvider, StringComparison.OrdinalIgnoreCase);

    private bool IsManifest => string.IsNullOrWhiteSpace(Provider) ||
                               string.Equals(Provider.Trim(), ManifestProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The address to read when the feature is switched on and usable; otherwise null. For GitHub this is the
    /// REST API's "latest release" endpoint, which skips drafts and pre-releases.
    /// </summary>
    public Uri? ResolveManifestUri()
    {
        if (!Enabled)
        {
            return null;
        }

        if (IsGitHub)
        {
            return TryParseGitHubRepository(GitHubRepository, out var owner, out var repo)
                ? new Uri($"https://api.github.com/repos/{owner}/{repo}/releases/latest")
                : null;
        }

        if (!IsManifest || string.IsNullOrWhiteSpace(Url))
        {
            return null;
        }

        return Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }

    internal static bool TryParseGitHubRepository(string? value, out string owner, out string repo)
    {
        owner = repo = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        const string prefix = "https://github.com/";
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            text = text[prefix.Length..];
        }

        text = text.TrimEnd('/');
        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }

        var match = GitHubRepositoryPattern().Match(text);
        if (!match.Success || match.Groups["repo"].Value is "." or "..")
        {
            return false;
        }

        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value;
        return true;
    }

    // GitHub's own rules: owners are alphanumeric with single hyphens, repositories add '.' and '_'.
    [GeneratedRegex("^(?<owner>[A-Za-z0-9](?:[A-Za-z0-9-]{0,38}))/(?<repo>[A-Za-z0-9._-]{1,100})$")]
    private static partial Regex GitHubRepositoryPattern();
}
