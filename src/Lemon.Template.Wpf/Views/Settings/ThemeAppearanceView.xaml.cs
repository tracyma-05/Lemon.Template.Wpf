using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.Settings;

[NavigationRegister(Constants.ThemeAppearance, Constants.MainRegion, typeof(UserControl), Constants.ThemeAppearanceIcon,
    ServiceLifetime.Singleton, DisplayOrder = 5)]
public partial class ThemeAppearanceView : UserControl
{
    public ThemeAppearanceView()
    {
        InitializeComponent();
    }
}
