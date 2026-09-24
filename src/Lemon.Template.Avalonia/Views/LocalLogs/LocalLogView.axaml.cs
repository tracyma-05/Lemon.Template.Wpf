using Avalonia.Controls;
using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Lemon.Template.Avalonia.Views.LocalLogs;

[NavigationRegister(Constants.AppLocalLog, Constants.MainRegion, typeof(UserControl), Constants.AppLocalLogIcon, ServiceLifetime.Transient, DisplayOrder = 20)]
public partial class LocalLogView : UserControl
{
    public LocalLogView()
    {
        InitializeComponent();

        // Jump to the newest entries whenever a log is (re)loaded.
        LogTextBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
            }
        };
    }
}
