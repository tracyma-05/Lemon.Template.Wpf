using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Lemon.Template.Wpf.Services.Updates;

namespace Lemon.Template.Wpf.Services.Shortcuts;

/// <summary>
/// A shortcut to the running app on the user's desktop. The app ships as a folder to unpack anywhere, so
/// this is how it gets a place to start from; the shortcut keeps working across automatic updates, which
/// replace the folder's contents but never move it.
/// </summary>
/// <remarks>
/// Written with the shell's own <c>IShellLinkW</c> / <c>IPersistFile</c> rather than <c>WScript.Shell</c>:
/// that one is late-bound and converts paths through the system ANSI code page, so a name with characters
/// outside it (Chinese on an English Windows) is saved as "??.lnk" or fails. IShellLinkW is UTF-16 throughout.
/// </remarks>
public static class DesktopShortcut
{
    /// <summary>
    /// Windows only, and only for an app that can be started directly: not when it runs through the
    /// <c>dotnet</c> host (<c>dotnet MyApp.dll</c>), whose shortcut would point at dotnet.exe.
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows() && AppInstallation.Current is not null;

    /// <summary>Where the shortcut named <paramref name="name"/> goes: <c>Desktop\{name}.lnk</c>.</summary>
    public static string PathFor(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), SafeFileName(name) + ".lnk");

    /// <summary>Creates the shortcut, replacing one of the same name, and returns its path.</summary>
    /// <exception cref="PlatformNotSupportedException">See <see cref="IsSupported"/>.</exception>
    /// <exception cref="COMException">The shell could not save it (desktop redirected to a read-only place, …).</exception>
    public static string Create(string name, string description)
    {
        if (!OperatingSystem.IsWindows() || AppInstallation.Current is not { } installation)
        {
            throw new PlatformNotSupportedException("Desktop shortcuts are created on Windows, for an app started from its own executable.");
        }

        var linkPath = PathFor(name);
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        Save(linkPath, installation.ExecutablePath, installation.RootPath, description);
        return linkPath;
    }

    [SupportedOSPlatform("windows")]
    internal static void Save(string linkPath, string target, string workingDirectory, string description)
    {
        object? shellLink = null;
        try
        {
            shellLink = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkClsid, throwOnError: true)!)
                ?? throw new COMException("Could not create a ShellLink object.");

            var link = (IShellLinkW)shellLink;
            link.SetPath(target);
            link.SetWorkingDirectory(workingDirectory);
            link.SetIconLocation(target, 0);
            // The tooltip in Explorer; the shell refuses one longer than INFOTIPSIZE.
            link.SetDescription(description.Length <= 200 ? description : description[..200]);

            ((IPersistFile)shellLink).Save(linkPath, fRemember: true);
        }
        finally
        {
            if (shellLink is not null && Marshal.IsComObject(shellLink))
            {
                Marshal.ReleaseComObject(shellLink);
            }
        }
    }

    internal static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        return cleaned.Length == 0 ? "App" : cleaned;
    }

    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        // Every member in vtable order, used or not: a missing one shifts all that follow.
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        // Inherited from IPersist; it comes first in the vtable.
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
