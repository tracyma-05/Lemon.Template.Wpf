using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// <c>{loc:Localize Key}</c> is a binding to the service indexer, not a one-off string: switching language
/// has to refresh every label in place. That only works while the service raises the property name the
/// Avalonia indexer binding listens for, which is exactly what a refactor could silently break.
/// </summary>
public class LocalizeExtensionTests
{
    [AvaloniaFact]
    public void Binding_RefreshesWhenTheCultureChanges()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;

        try
        {
            localization.SetCulture(CultureInfo.GetCultureInfo("en"));

            var label = new TextBlock();
            label.Bind(TextBlock.TextProperty, LocalizeExtension.CreateBinding("Theme_Title"));
            var window = new Window { Content = label };
            window.Show();

            var english = label.Text;
            Assert.Equal(localization.GetString("Theme_Title"), english);

            localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));

            Assert.Equal(localization.GetString("Theme_Title"), label.Text);
            Assert.NotEqual(english, label.Text);

            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
