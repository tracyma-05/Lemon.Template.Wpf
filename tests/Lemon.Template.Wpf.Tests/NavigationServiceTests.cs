using System;
using System.Windows.Controls;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

public class NavigationServiceTests
{
    private static NavigationService CreateService() =>
        new(new ServiceCollection().BuildServiceProvider());

    [Fact]
    public void RegisterRoute_RejectsTypesThatAreNotUserControls()
    {
        var service = CreateService();

        var ex = Assert.Throws<ArgumentException>(
            () => service.RegisterRoute("Theme", typeof(string), "MainRegion"));

        Assert.Contains(nameof(String), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterRoute_AcceptsUserControlSubclasses()
    {
        var service = CreateService();

        service.RegisterRoute("Theme", typeof(FakeView), "MainRegion");

        // Region is still missing, so navigation must fail on the region -- proving the route registered.
        var ex = Assert.Throws<InvalidOperationException>(() => service.Navigate("Theme"));
        Assert.Contains("MainRegion", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Navigate_RejectsMissingRouteName(string? routeName)
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.Navigate(routeName!));
    }

    [Fact]
    public void Navigate_ReportsTheRouteNameWhenUnregistered()
    {
        var service = CreateService();

        var ex = Assert.Throws<InvalidOperationException>(() => service.Navigate("NoSuchRoute"));

        Assert.Contains("NoSuchRoute", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveView_RejectsNull()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() => service.RemoveView(null!));
    }

    private sealed class FakeView : UserControl
    {
    }
}
