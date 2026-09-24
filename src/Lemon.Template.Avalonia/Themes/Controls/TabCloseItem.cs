using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Windows.Input;

namespace Lemon.Template.Avalonia.Themes.Controls
{
    /// <summary>
    /// A tab with a close button in its header (see the <c>TabItem.closable</c> style in TabControl.axaml).
    /// </summary>
    /// <remarks>
    /// Styled as a plain <see cref="TabItem"/> so it keeps the Material look; the close button lives in the
    /// header template, and its click is recognised by the <see cref="CloseButtonClass"/> class.
    /// </remarks>
    internal class TabCloseItem : TabItem
    {
        public const string CloseButtonClass = "tab-close";

        public static readonly StyledProperty<ICommand?> CloseCommandProperty =
            AvaloniaProperty.Register<TabCloseItem, ICommand?>(nameof(CloseCommand));

        public static readonly StyledProperty<object?> CloseCommandParameterProperty =
            AvaloniaProperty.Register<TabCloseItem, object?>(nameof(CloseCommandParameter));

        public static readonly RoutedEvent<RoutedEventArgs> CloseClickEvent =
            RoutedEvent.Register<TabCloseItem, RoutedEventArgs>(nameof(CloseClick), RoutingStrategies.Bubble);

        public TabCloseItem()
        {
            Classes.Add("closable");
            AddHandler(Button.ClickEvent, OnButtonClick);
        }

        protected override Type StyleKeyOverride => typeof(TabItem);

        public ICommand? CloseCommand
        {
            get => GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public object? CloseCommandParameter
        {
            get => GetValue(CloseCommandParameterProperty);
            set => SetValue(CloseCommandParameterProperty, value);
        }

        public event EventHandler<RoutedEventArgs> CloseClick
        {
            add => AddHandler(CloseClickEvent, value);
            remove => RemoveHandler(CloseClickEvent, value);
        }

        private void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not Button button || !button.Classes.Contains(CloseButtonClass))
            {
                return;
            }

            e.Handled = true;
            OnCloseClick();
        }

        protected virtual void OnCloseClick()
        {
            RaiseEvent(new RoutedEventArgs(CloseClickEvent, this));

            if (CloseCommand?.CanExecute(CloseCommandParameter) ?? false)
            {
                CloseCommand.Execute(CloseCommandParameter);
            }
        }
    }
}
