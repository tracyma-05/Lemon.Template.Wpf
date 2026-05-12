using Microsoft.Extensions.Configuration;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Lemon.Template.Wpf.NuGetPublish;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var publisherDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)
                               ?? Directory.GetCurrentDirectory();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(publisherDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddUserSecrets("2bd34198-f241-4881-b81e-a26546827f68")
                .AddEnvironmentVariables(prefix: "NUGET_PUBLISH_")
                .AddCommandLine(args)
                .Build();

            var options = configuration.GetSection(NuGetPublishOptions.SectionName).Get<NuGetPublishOptions>()
                          ?? new NuGetPublishOptions();

            var repoRoot = RepositoryRootResolver.Resolve(options.RepositoryRoot);
            var packProject = TemplatePackPathResolver.Resolve(repoRoot, options.TemplatePackProjectRelative);
            var outDir = Path.GetFullPath(Path.Combine(repoRoot, options.PackOutputDirectoryRelative));

            var apiKey = options.ApiKey?.Trim() ?? "";
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException(
                    "Missing API key. Set NuGetPublish:ApiKey in secret.json (next to appsettings.json or cwd), appsettings.json, or environment variable NUGET_PUBLISH__ApiKey.");

            Console.WriteLine($"Repository root: {repoRoot}");
            Console.WriteLine($"Packing: {packProject}");
            Console.WriteLine($"Output:    {outDir}");

            if (!options.SkipPreBuild)
            {
                IReadOnlyDictionary<string, string>? buildProps = null;
                if (!string.IsNullOrWhiteSpace(options.PackageVersion))
                {
                    var v = options.PackageVersion.Trim();
                    buildProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Version"] = v,
                        ["PackageVersion"] = v,
                    };
                }

                Console.WriteLine("Pre-build: compiling solution with latest sources (and optional Version override)...");
                RepositoryRootResolver.RunDotnetBuildSolution(
                    repoRoot,
                    options.SolutionRelativePath,
                    options.PreBuildConfiguration,
                    buildProps);
            }

            RepositoryRootResolver.RunDotnetPack(repoRoot, packProject, outDir, options.PackageVersion);

            var nupkg = Directory.EnumerateFiles(outDir, "*.nupkg", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(nupkg))
                throw new InvalidOperationException($"No .nupkg found under '{outDir}' after pack.");

            Console.WriteLine($"Package:   {nupkg}");

            await VerifyTemplatePackageAsync(nupkg, CancellationToken.None).ConfigureAwait(false);

            await PushToNuGetAsync(
                nupkg,
                options.PackageSource.Trim(),
                apiKey,
                options.SkipDuplicate,
                options.PushTimeoutSeconds,
                CancellationToken.None).ConfigureAwait(false);

            Console.WriteLine("Push completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task VerifyTemplatePackageAsync(string nupkgPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(nupkgPath);
        using var reader = new PackageArchiveReader(stream, leaveStreamOpen: false);

        var types = reader.NuspecReader.GetPackageTypes();
        var isTemplate = types.Any(t => string.Equals(t.Name, "Template", StringComparison.OrdinalIgnoreCase));
        if (!isTemplate)
            Console.WriteLine("Warning: package does not declare PackageType 'Template' in the manifest.");

        var id = reader.NuspecReader.GetId();
        var version = reader.NuspecReader.GetVersion();
        Console.WriteLine($"Manifest: {id} {version}");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task PushToNuGetAsync(
        string nupkgPath,
        string packageSource,
        string apiKey,
        bool skipDuplicate,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        ILogger logger = NullLogger.Instance;

        var repository = Repository.Factory.GetCoreV3(packageSource);
        var resource = await repository.GetResourceAsync<PackageUpdateResource>(cancellationToken)
                       .ConfigureAwait(false);

        if (resource is null)
            throw new InvalidOperationException("Feed does not support package push (no PackageUpdateResource).");

        Console.WriteLine($"Pushing to {packageSource} ...");

        await resource.Push(
            new[] { nupkgPath },
            symbolSource: string.Empty,
            timeoutInSecond: timeoutSeconds,
            disableBuffering: false,
            getApiKey: _ => apiKey,
            getSymbolApiKey: _ => string.Empty,
            noServiceEndpoint: false,
            skipDuplicate: skipDuplicate,
            symbolPackageUpdateResource: null,
            allowInsecureConnections: false,
            log: logger).ConfigureAwait(false);
    }
}