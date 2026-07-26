using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.Settings;

[NavigationRegister(Constants.Language, Constants.MainRegion, typeof(UserControl), Constants.LanguageIcon, ServiceLifetime.Transient, DisplayOrder = 2)]
public partial class LanguageView : UserControl
{
    public LanguageView()
    {
        InitializeComponent();
    }
}
