using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Themes.Controls
{
    /// <summary>Tab region whose generated containers are <see cref="TabCloseItem"/>s.</summary>
    internal class TabControl : global::Avalonia.Controls.TabControl
    {
        protected override Type StyleKeyOverride => typeof(global::Avalonia.Controls.TabControl);

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            return NeedsContainer<TabCloseItem>(item, out recycleKey);
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new TabCloseItem();
        }
    }
}
