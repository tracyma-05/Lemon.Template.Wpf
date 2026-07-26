using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Lemon.Template.Wpf.Infrastructures.Shell;

internal static class DesktopShortcutHelper
{
    /// <summary>
    /// Recreates the desktop shortcut on each launch so target path and icon stay current.
    /// </summary>
    public static void EnsureDesktopShortcut()
    {
        var targetPath = ResolveLaunchTargetPath();
        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
        {
            return;
        }

        var shortcutName = $"{GetShortcutDisplayName()}.lnk";
        var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutPath = Path.Combine(desktopDir, shortcutName);

        try
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            CreateShortcutViaWScript(shortcutPath, targetPath, GetShortcutDisplayName());
        }
        catch
        {
            // Ignore: no desktop, COM disabled, etc.
        }
    }

    private static void CreateShortcutViaWScript(string shortcutPath, string targetPath, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            return;
        }

        object shell = Activator.CreateInstance(shellType)!;
        try
        {
            object shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath])!;

            var shortcutType = shortcut.GetType();
            var workingDir = Path.GetDirectoryName(targetPath) ?? string.Empty;

            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [workingDir]);
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [$"{targetPath},0"]);
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, [description]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, args: null);

            if (Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }
        }
        finally
        {
            if (Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static string? ResolveLaunchTargetPath()
    {
        var assemblyLocation = typeof(App).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var appHost = Path.ChangeExtension(assemblyLocation, ".exe");
            if (File.Exists(appHost))
            {
                return appHost;
            }
        }

        return Environment.ProcessPath;
    }

    private static string GetShortcutDisplayName()
    {
        var entry = Assembly.GetEntryAssembly() ?? typeof(App).Assembly;
        var title = entry.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return entry.GetName().Name ?? "Application";
    }
}
