using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Views.Settings;

[NavigationRegister(Constants.ThemeAppearance, Constants.MainRegion, typeof(UserControl), Constants.ThemeAppearanceIcon,
    ServiceLifetime.Singleton, DisplayOrder = 5)]
public partial class ThemeAppearanceView : UserControl
{
    public ThemeAppearanceView()
    {
        InitializeComponent();
    }
}
