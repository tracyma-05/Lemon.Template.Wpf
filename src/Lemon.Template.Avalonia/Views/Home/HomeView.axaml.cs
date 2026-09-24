using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Views.Home;

/// <summary>
/// Landing page. Registered with a single-segment key so it renders as a top-level menu entry with no
/// children, and with <c>DisplayOrder = 0</c> so it sorts above every group.
/// </summary>
[NavigationRegister(Constants.Home, Constants.MainRegion, typeof(UserControl), Constants.HomeIcon,
    ServiceLifetime.Singleton, DisplayOrder = 0)]
public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }
}
