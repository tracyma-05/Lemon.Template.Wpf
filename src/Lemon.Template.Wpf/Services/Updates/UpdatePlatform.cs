using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Lemon.Template.Wpf.Services.Updates;

/// <summary>
/// Picks the package that suits this computer when a release ships one per platform.
/// </summary>
/// <remarks>
/// Platforms are named like .NET runtime identifiers: <c>win-x64</c>, <c>win-arm64</c>, <c>osx-arm64</c>,
/// <c>osx-x64</c>, <c>linux-x64</c>; <c>any</c> is a package that runs everywhere. A bare operating system
/// (<c>osx</c>) stands for a package that covers every architecture of it, such as a universal macOS build.
/// </remarks>
public static partial class UpdatePlatform
{
    public const string Any = "any";

    // Longest first, so ".tar.gz" wins over ".gz".
    private static readonly string[] PackageExtensions =
        [".tar.gz", ".tgz", ".zip", ".7z", ".exe", ".msi", ".msix", ".dmg", ".pkg", ".appimage", ".deb", ".rpm"];

    // Installers are preferred over archives when a release offers both for the same platform.
    private static readonly string[] InstallerExtensions = [".msi", ".msix", ".exe", ".dmg", ".pkg"];

    /// <summary>This process's platform, e.g. <c>win-x64</c> or <c>osx-arm64</c>; <c>any</c> when it has no name here.</summary>
    public static string Current { get; } = Describe(RuntimeInformation.ProcessArchitecture);

    /// <summary>
    /// Keys to look for, best first: the exact platform; the x64 build on arm64 (Windows on Arm emulation,
    /// Rosetta 2 on Apple Silicon); a build for the whole operating system; a package for any platform.
    /// </summary>
    public static IReadOnlyList<string> Candidates(string platform)
    {
        var candidates = new List<string>();
        var parts = platform.Split('-', 2);
        if (parts.Length == 2)
        {
            candidates.Add(platform);
            if (parts[1] == "arm64")
            {
                candidates.Add($"{parts[0]}-x64");
            }

            candidates.Add(parts[0]);
        }

        candidates.Add(Any);
        return candidates;
    }

    /// <summary>The first candidate the release has a package for.</summary>
    public static Uri? Select(IReadOnlyDictionary<string, Uri> downloads, IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (downloads.TryGetValue(candidate, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    /// <summary>True for the file types a release asset can be installed from (checksums, signatures and the like are not).</summary>
    public static bool IsPackage(string fileName) =>
        PackageExtensions.Any(x => fileName.EndsWith(x, StringComparison.OrdinalIgnoreCase));

    /// <summary>Lower is better; see <see cref="InstallerExtensions"/>.</summary>
    public static int PackageRank(string fileName) =>
        InstallerExtensions.Any(x => fileName.EndsWith(x, StringComparison.OrdinalIgnoreCase)) ? 0 : 1;

    /// <summary>
    /// The platform a file name mentions, such as <c>MyApp-1.2.0-osx-arm64.zip</c> → <c>osx-arm64</c>;
    /// null when it names no operating system, or an architecture other than x64, x86 and arm64.
    /// </summary>
    /// <remarks>
    /// Also understands the spellings other build tools use: windows / macos / mac / darwin, amd64 /
    /// x86_64 / aarch64 / 386, win64, and "universal" for a macOS build covering both architectures.
    /// </remarks>
    public static string? FromFileName(string fileName)
    {
        var name = fileName.ToLowerInvariant();
        var extension = PackageExtensions.FirstOrDefault(name.EndsWith);
        if (extension is not null)
        {
            name = name[..^extension.Length];
        }

        name = name.Replace("x86_64", "x64").Replace("amd64", "x64").Replace("aarch64", "arm64");

        string? os = null;
        string? architecture = null;
        foreach (var token in TokenSeparator().Split(name))
        {
            switch (token)
            {
                case "win" or "windows":
                    os = "win";
                    break;
                case "win64":
                    os = "win";
                    architecture ??= "x64";
                    break;
                case "osx" or "macos" or "mac" or "darwin":
                    os = "osx";
                    break;
                case "linux":
                    os = "linux";
                    break;
                case "x64" or "x86" or "arm64":
                    architecture = token;
                    break;
                case "386" or "i386" or "i686":
                    architecture = "x86";
                    break;
                case "arm" or "armv6" or "armv7" or "armhf" or "ppc64le" or "s390x" or "riscv64" or "loong64":
                    // An architecture .NET desktop apps are not published for here: never offer it.
                    return null;
            }
        }

        if (os is null)
        {
            return null;
        }

        return architecture is null ? os : $"{os}-{architecture}";
    }

    private static string Describe(Architecture architecture)
    {
        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : null;

        var arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

        return os is null || arch is null ? Any : $"{os}-{arch}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex TokenSeparator();
}
