namespace Lemon.Template.Wpf.NuGetPublish;

internal static class TemplatePackPathResolver
{
    public const string DefaultTemplatePackFileName = "Lemon.Template.Wpf.TemplatePack.csproj";

    /// <summary>
    /// Resolves the template pack csproj: configured path, default at repo root, or a single *TemplatePack*.csproj in the repo root.
    /// </summary>
    public static string Resolve(string repositoryRoot, string templatePackProjectRelative)
    {
        var tried = new List<string>();

        if (Path.IsPathRooted(templatePackProjectRelative))
        {
            var abs = Path.GetFullPath(templatePackProjectRelative);
            tried.Add(abs);
            if (File.Exists(abs))
                return abs;
        }
        else
        {
            var combined = Path.GetFullPath(Path.Combine(repositoryRoot, templatePackProjectRelative));
            tried.Add(combined);
            if (File.Exists(combined))
                return combined;
        }

        var fallback = Path.GetFullPath(Path.Combine(repositoryRoot, DefaultTemplatePackFileName));
        if (!tried.Contains(fallback, StringComparer.OrdinalIgnoreCase))
            tried.Add(fallback);
        if (File.Exists(fallback))
            return fallback;

        var matches = Directory.GetFiles(repositoryRoot, "*TemplatePack*.csproj", SearchOption.TopDirectoryOnly);
        if (matches.Length == 1)
            return Path.GetFullPath(matches[0]);

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple *TemplatePack*.csproj files found at the repository root; set NuGetPublish:TemplatePackProjectRelative explicitly. Found: "
                + string.Join(", ", matches));
        }

        throw new FileNotFoundException(
            "Template pack project not found. Tried: " + string.Join(" ; ", tried));
    }
}
