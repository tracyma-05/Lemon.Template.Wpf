namespace Lemon.Template.Wpf.NuGetPublish;

public sealed class NuGetPublishOptions
{
    public const string SectionName = "NuGetPublish";

    /// <summary>NuGet service index URL (nuget.org default).</summary>
    public string PackageSource { get; set; } = "https://api.nuget.org/v3/index.json";

    /// <summary>API key for the feed. Prefer <c>secret.json</c> over storing here.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Repository root containing the template pack project. Empty = auto-detect.</summary>
    public string RepositoryRoot { get; set; } = "";

    public string TemplatePackProjectRelative { get; set; } = "Lemon.Template.Wpf.TemplatePack.csproj";

    /// <summary>Solution to build before pack (latest sources + same Version as common.props).</summary>
    public string SolutionRelativePath { get; set; } = "Lemon.Template.Wpf.sln";

    /// <summary>When true, skips <c>dotnet build</c> on the solution before packing.</summary>
    public bool SkipPreBuild { get; set; }

    public string PreBuildConfiguration { get; set; } = "Release";

    public string PackOutputDirectoryRelative { get; set; } = "artifacts";

    /// <summary>Passed to MSBuild as <c>PackageVersion</c> when non-empty; otherwise uses the value in the template pack csproj.</summary>
    public string PackageVersion { get; set; } = "";

    public bool SkipDuplicate { get; set; } = true;

    public int PushTimeoutSeconds { get; set; } = 600;
}
