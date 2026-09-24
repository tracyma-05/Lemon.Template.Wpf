using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Views.Settings;

[NavigationRegister(Constants.Language, Constants.MainRegion, typeof(UserControl), Constants.LanguageIcon, ServiceLifetime.Transient, DisplayOrder = 2)]
public partial class LanguageView : UserControl
{
    public LanguageView()
    {
        InitializeComponent();
    }
}
