using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Navigations;
using Lemon.Template.Avalonia.Infrastructures.Shell;
using Lemon.Template.Avalonia.Models;
using System.Reflection;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.ViewModels.Home;

public sealed partial class HomeViewModel : ObservableObject, ISingletonDependency
{
    private const string RepositoryUrl = "https://github.com/tracyma-05/Lemon.Template.Wpf";
    private const string MaterialDesignUrl = "https://github.com/AvaloniaCommunity/Material.Avalonia";

    private readonly IMenuNavigator _menuNavigator;

    public HomeViewModel(IMenuNavigator menuNavigator)
    {
        _menuNavigator = menuNavigator;

        Shortcuts = BuildShortcuts();
        Links =
        [
            new HomeLink("Home_Link_Repository", "Github", RepositoryUrl),
            new HomeLink("Home_Link_MaterialDesign", "Palette", MaterialDesignUrl),
        ];
    }

    public IReadOnlyList<HomeShortcut> Shortcuts { get; }

    public IReadOnlyList<HomeLink> Links { get; }

    /// <summary>
    /// Read from the entry assembly rather than hard-coded, so a project generated from this template
    /// shows its own name here without editing the view.
    /// </summary>
    public string ApplicationTitle { get; } = ResolveApplicationTitle();

    public string ApplicationVersion { get; } = ResolveApplicationVersion();

    /// <summary>
    /// Built in code rather than listed in XAML because the set depends on template symbols
    /// (see <c>EnableHangfire</c>).
    /// </summary>
    public IReadOnlyList<string> TechStack { get; } =
    [
        ".NET 10 / Avalonia",
        "Material.Avalonia",
        "ABP + Autofac",
        "CommunityToolkit.Mvvm",
        "Serilog",
        "SQLite",
#if (EnableHangfire)
        "Hangfire",
#endif
    ];

    /// <summary>Hero action: jumps to the page a new project is most likely to open first.</summary>
    [RelayCommand]
    private void GetStarted() => _menuNavigator.NavigateTo(Constants.ThemeAppearance);

    [RelayCommand]
    private void OpenShortcut(HomeShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return;
        }

        _menuNavigator.NavigateTo(shortcut.RegisterGroup);
    }

    [RelayCommand]
    private static void OpenUrl(string? url) => BrowserLauncher.TryOpen(url);

    private static List<HomeShortcut> BuildShortcuts()
    {
        var shortcuts = new List<HomeShortcut>
        {
            new(Constants.ThemeAppearance, "Palette", "Home_Shortcut_Theme"),
            new(Constants.Language, "Translate", "Home_Shortcut_Language"),
            new(Constants.AppLocalLog, "TextBoxSearchOutline", "Home_Shortcut_LocalLogs"),
        };

#if (EnableHangfire)
        shortcuts.Add(new HomeShortcut(Constants.Cron, "Schedule", "Home_Shortcut_Cron"));
#endif

        return shortcuts;
    }

    private static string ResolveApplicationTitle()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(HomeViewModel).Assembly;
        return assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
               ?? assembly.GetName().Name
               ?? "Application";
    }

    private static string ResolveApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(HomeViewModel).Assembly;
        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
