using System.Windows;

namespace Lemon.Template.Wpf.Themes.Controls
{
    internal class TabControl : System.Windows.Controls.TabControl
    {
        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is TabCloseItem;
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TabCloseItem();
        }
    }
}