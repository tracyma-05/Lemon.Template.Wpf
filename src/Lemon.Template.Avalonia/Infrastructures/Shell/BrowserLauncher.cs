using Serilog;
using System.Diagnostics;

namespace Lemon.Template.Avalonia.Infrastructures.Shell;

/// <summary>Opens web links in the user's default browser on Windows and macOS.</summary>
internal static class BrowserLauncher
{
    /// <returns><c>false</c> when the URL was refused or the browser could not be started (both logged).</returns>
    public static bool TryOpen(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Restricted to http(s) on purpose: UseShellExecute happily launches file paths and custom
        // protocol handlers, which is never what a link button on a page should do.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Log.Warning("Refusing to open '{Url}': only http and https links are supported.", url);
            return false;
        }

        try
        {
            // With UseShellExecute, .NET hands the URL to the shell on Windows and to `open` on macOS.
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not open {Url} in the default browser.", uri);
            return false;
        }
    }
}
