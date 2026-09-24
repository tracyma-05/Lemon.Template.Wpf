using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>Side-menu styles from Themes/Navigation.axaml, applied through the real App styles.</summary>
public class NavigationStyleTests
{
    /// <summary>
    /// Collapsed to 70 px the menu only has room for icons: the group chevron has to go too, or it is drawn on
    /// top of the group icon. The chevron sits two templates deep, which is easy to break with a theme update.
    /// </summary>
    [AvaloniaFact]
    public void CollapsedMenu_HidesTheGroupChevron()
    {
        var expander = new Expander
        {
            Classes = { "nav-group" },
            Header = new TextBlock { Text = "Settings" },
            Content = new TextBlock { Text = "Theme" },
        };
        var menu = new Border { Child = expander };
        var window = new Window { Width = 240, Height = 200, Content = menu };
        window.Show();

        var chevron = expander.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "PART_ExpandIcon");
        Assert.Equal(1d, chevron.Opacity);

        menu.Classes.Add("menu-collapsed");
        Settle();
        Assert.Equal(0d, chevron.Opacity);

        menu.Classes.Remove("menu-collapsed");
        Settle();
        Assert.Equal(1d, chevron.Opacity);

        window.Close();
    }

    /// <summary>Runs the fade transition to its end.</summary>
    private static void Settle()
    {
        for (var i = 0; i < 30; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
