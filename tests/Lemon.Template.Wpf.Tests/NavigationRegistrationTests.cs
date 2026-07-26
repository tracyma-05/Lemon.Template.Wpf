using System.Linq;
using System.Windows.Controls;
using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

// Fixtures declared out of display order on purpose, so the assertions below prove sorting rather than
// just reflecting whatever order the assembly happens to enumerate types in.
[NavigationRegister("Alpha/Two", Constants.MainRegion, typeof(UserControl), "AlphaIcon/TwoIcon", DisplayOrder = 20)]
internal sealed class AlphaTwoFixture { }

[NavigationRegister("Alpha/One", Constants.MainRegion, typeof(UserControl), "AlphaIcon/OneIcon", DisplayOrder = 10)]
internal sealed class AlphaOneFixture { }

[NavigationRegister("Beta/Solo", Constants.MainRegion, typeof(UserControl), "BetaIcon/SoloIcon", DisplayOrder = 5)]
internal sealed class BetaSoloFixture { }

// Single-segment key: a top-level page that is its own menu entry.
[NavigationRegister("Gamma", Constants.MainRegion, typeof(UserControl), "GammaIcon", DisplayOrder = 1)]
internal sealed class GammaFixture { }

/// <summary>
/// Menu construction is attribute-driven and writes into the shared <see cref="Constants.NavigationItems"/>
/// collection; the grouping and DisplayOrder rules here are what the side navigation renders.
/// </summary>
public class NavigationRegistrationTests
{
    private static ServiceProvider BuildProviderWithNavigation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddRouteServiceFromAssembly_GroupsChildrenUnderTheirTopLevelMenu()
    {
        Constants.NavigationItems.Clear();
        using var provider = BuildProviderWithNavigation();

        ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(provider, typeof(AlphaOneFixture).Assembly);

        var alpha = Assert.Single(Constants.NavigationItems, item => item.Title == "Alpha");
        Assert.Equal(new[] { "One", "Two" }, alpha.Items.Select(i => i.Title));

        var beta = Assert.Single(Constants.NavigationItems, item => item.Title == "Beta");
        Assert.Equal(new[] { "Solo" }, beta.Items.Select(i => i.Title));
    }

    [Fact]
    public void AddRouteServiceFromAssembly_OrdersTopLevelMenusByTheirSmallestChildDisplayOrder()
    {
        Constants.NavigationItems.Clear();
        using var provider = BuildProviderWithNavigation();

        ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(provider, typeof(AlphaOneFixture).Assembly);

        // Gamma is 1, Beta's only child is 5, Alpha's smallest is 10 — so declaration and alphabetical
        // order are both irrelevant next to DisplayOrder.
        Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, Constants.NavigationItems.Select(i => i.Title));
        Assert.Equal(1, Constants.NavigationItems[0].DisplayOrder);
        Assert.Equal(5, Constants.NavigationItems[1].DisplayOrder);
        Assert.Equal(10, Constants.NavigationItems[2].DisplayOrder);
    }

    [Fact]
    public void AddRouteServiceFromAssembly_KeepsASingleSegmentKeyAsAChildlessTopLevelEntry()
    {
        Constants.NavigationItems.Clear();
        using var provider = BuildProviderWithNavigation();

        ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(provider, typeof(AlphaOneFixture).Assembly);

        var gamma = Assert.Single(Constants.NavigationItems, item => item.Title == "Gamma");

        // No children is what makes the shell render it with NavigationChildlessItemTemplate.
        Assert.Empty(gamma.Items);
        Assert.Equal("GammaIcon", gamma.Icon);

        var navigationService = provider.GetRequiredService<INavigationService>();
        var ex = Assert.Throws<System.InvalidOperationException>(() => navigationService.Navigate("Gamma"));
        Assert.Contains(Constants.MainRegion, ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AddRouteServiceFromAssembly_TakesGroupIconFromFirstSegmentAndChildIconFromLast()
    {
        Constants.NavigationItems.Clear();
        using var provider = BuildProviderWithNavigation();

        ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(provider, typeof(AlphaOneFixture).Assembly);

        var alpha = Constants.NavigationItems.Single(item => item.Title == "Alpha");
        Assert.Equal("AlphaIcon", alpha.Icon);
        Assert.Equal("OneIcon", alpha.Items.Single(i => i.Title == "One").Icon);
        Assert.Equal("TwoIcon", alpha.Items.Single(i => i.Title == "Two").Icon);
    }

    [Fact]
    public void AddRouteServiceFromAssembly_RegistersANavigableRouteForEveryAttribute()
    {
        Constants.NavigationItems.Clear();
        using var provider = BuildProviderWithNavigation();

        ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(provider, typeof(AlphaOneFixture).Assembly);

        var navigationService = provider.GetRequiredService<INavigationService>();

        // The route resolves; only the (unregistered) region is missing at this point.
        var ex = Assert.Throws<System.InvalidOperationException>(() => navigationService.Navigate("One"));
        Assert.Contains(Constants.MainRegion, ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AddNavigationServiceFromAssembly_RegistersOneKeyedServicePerAttribute()
    {
        var services = new ServiceCollection();

        services.AddNavigationServiceFromAssembly(typeof(AlphaOneFixture).Assembly);

        var keyed = services
            .Where(d => d.IsKeyedService && d.ServiceType == typeof(UserControl))
            .Select(d => d.ServiceKey as string)
            .ToList();

        Assert.Contains($"One.{Constants.MainRegion}", keyed);
        Assert.Contains($"Two.{Constants.MainRegion}", keyed);
        Assert.Contains($"Solo.{Constants.MainRegion}", keyed);
        Assert.Contains($"Gamma.{Constants.MainRegion}", keyed);
    }
}
