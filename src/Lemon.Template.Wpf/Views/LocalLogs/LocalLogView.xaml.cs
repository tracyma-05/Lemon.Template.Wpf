using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.LocalLogs;

[NavigationRegister(Constants.AppLocalLog, Constants.MainRegion, typeof(UserControl), Constants.AppLocalLogIcon, ServiceLifetime.Transient, DisplayOrder = 20)]
public partial class LocalLogView : UserControl
{
    public LocalLogView()
    {
        InitializeComponent();
    }

    private void LogTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        LogTextBox.ScrollToEnd();
    }
}