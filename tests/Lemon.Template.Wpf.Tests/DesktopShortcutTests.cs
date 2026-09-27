using System.IO;
using System.Text;
using Lemon.Template.Wpf.Services.Shortcuts;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

/// <summary>The shell link is written through IShellLinkW, so non-ASCII names survive any ANSI code page.</summary>
public sealed class DesktopShortcutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shortcut-tests-" + Guid.NewGuid().ToString("N"));

    public DesktopShortcutTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Save_writes_a_shell_link_to_the_target_even_with_a_chinese_name()
    {
        var target = Path.Combine(_root, "应用", "MyApp.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "exe");
        var link = Path.Combine(_root, "代码项目启动器.lnk");

        DesktopShortcut.Save(link, target, Path.GetDirectoryName(target)!, "打开应用");

        var bytes = File.ReadAllBytes(link);
        // Shell link header: size 0x4C, then the ShellLink CLSID.
        Assert.Equal(0x4C, BitConverter.ToInt32(bytes, 0));
        Assert.Contains(target, Encoding.Unicode.GetString(bytes), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_replaces_an_existing_shortcut()
    {
        var target = Path.Combine(_root, "MyApp.exe");
        File.WriteAllText(target, "exe");
        var link = Path.Combine(_root, "MyApp.lnk");
        File.WriteAllText(link, "stale");

        DesktopShortcut.Save(link, target, _root, "MyApp");

        Assert.Equal(0x4C, BitConverter.ToInt32(File.ReadAllBytes(link), 0));
    }

    [Theory]
    [InlineData("MyApp", "MyApp")]
    [InlineData("My:App?", "My_App_")]
    [InlineData(" . ", "App")]
    public void Names_are_made_safe_for_a_file(string name, string expected)
    {
        Assert.Equal(expected, DesktopShortcut.SafeFileName(name));
    }

    [Fact]
    public void Shortcut_goes_to_the_desktop()
    {
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MyApp.lnk"),
            DesktopShortcut.PathFor("MyApp"));
    }
}
