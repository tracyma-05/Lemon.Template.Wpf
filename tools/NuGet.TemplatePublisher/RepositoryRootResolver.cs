using System.Diagnostics;

namespace Lemon.Template.Wpf.NuGetPublish;

internal static class RepositoryRootResolver
{
    private const string MarkerFile = "Lemon.Template.Wpf.TemplatePack.csproj";

    public static string Resolve(string? configuredRoot, string? startDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var full = Path.GetFullPath(configuredRoot);
            if (!File.Exists(Path.Combine(full, MarkerFile)))
                throw new DirectoryNotFoundException($"RepositoryRoot '{full}' does not contain '{MarkerFile}'.");
            return full;
        }

        foreach (var root in EnumerateCandidates(startDirectory))
        {
            if (File.Exists(Path.Combine(root, MarkerFile)))
                return root;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate '{MarkerFile}'. Set NuGetPublish:RepositoryRoot in appsettings.json or run from the repository directory.");
    }

    private static IEnumerable<string> EnumerateCandidates(string? startDirectory)
    {
        var dirs = new List<string?>();
        if (!string.IsNullOrEmpty(startDirectory))
            dirs.Add(startDirectory);
        dirs.Add(Directory.GetCurrentDirectory());
        dirs.Add(AppContext.BaseDirectory);

        foreach (var d in dirs.Where(s => !string.IsNullOrEmpty(s)).Distinct())
        {
            var dir = new DirectoryInfo(Path.GetFullPath(d!));
            while (dir is not null)
            {
                yield return dir.FullName;
                dir = dir.Parent;
            }
        }
    }

    public static void RunDotnetPack(string repositoryRoot, string packProjectPath, string outputDirectory, string? packageVersion)
    {
        Directory.CreateDirectory(outputDirectory);

        var args = new List<string>
        {
            "pack",
            $"\"{packProjectPath}\"",
            "-c", "Release",
            "-o", $"\"{outputDirectory}\"",
            "--nologo",
            "-p:ContinuousIntegrationBuild=true",
            "-p:NoDefaultExcludes=true"
        };

        if (!string.IsNullOrWhiteSpace(packageVersion))
        {
            var v = packageVersion.Trim();
            args.AddRange(["-p:Version=" + v, "-p:PackageVersion=" + v]);
        }

        RunDotnet(repositoryRoot, args, "dotnet pack");
    }

    /// <summary>Builds the solution so sources (and shared <c>Version</c> from common.props) are up to date before packing the template.</summary>
    public static void RunDotnetBuildSolution(
        string repositoryRoot,
        string solutionRelativePath,
        string configuration,
        IReadOnlyDictionary<string, string>? extraMsBuildProperties = null)
    {
        var sln = Path.GetFullPath(Path.Combine(repositoryRoot, solutionRelativePath));
        if (!File.Exists(sln))
            throw new FileNotFoundException("Solution file not found for pre-build.", sln);

        var args = new List<string>
        {
            "build",
            $"\"{sln}\"",
            "-c", configuration,
            "--nologo",
            "-v", "minimal"
        };

        if (extraMsBuildProperties is not null)
        {
            foreach (var kv in extraMsBuildProperties)
                args.Add($"-p:{kv.Key}={kv.Value}");
        }

        RunDotnet(repositoryRoot, args, "dotnet build");
    }

    private static void RunDotnet(string workingDirectory, List<string> args, string stepLabel)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = string.Join(" ", args),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Console.WriteLine($"{stepLabel}: dotnet {string.Join(" ", args)}");

        using var process = Process.Start(psi)
                        ?? throw new InvalidOperationException("Failed to start dotnet.");

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{stepLabel} failed with exit code {process.ExitCode}.");
    }
}
