using System.Windows;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public class Dialog
    {
        /// <summary>
        /// Identifies the WindowStyle attached property.
        /// </summary>
        /// <remarks>
        /// This attached property is used to specify the style of a <see cref="IDialogWindow"/>.
        /// </remarks>
        public static readonly DependencyProperty WindowStyleProperty =
            DependencyProperty.RegisterAttached("WindowStyle", typeof(Style), typeof(Dialog), new PropertyMetadata(null));

        /// <summary>
        /// Gets the value for the <see cref="WindowStyleProperty"/> attached property.
        /// </summary>
        /// <param name="obj">The target element.</param>
        /// <returns>The <see cref="WindowStyleProperty"/> attached to the <paramref name="obj"/> element.</returns>
        public static Style GetWindowStyle(DependencyObject obj)
        {
            return (Style)obj.GetValue(WindowStyleProperty);
        }

        /// <summary>
        /// Sets the <see cref="WindowStyleProperty"/> attached property.
        /// </summary>
        /// <param name="obj">The target element.</param>
        /// <param name="value">The Style to attach.</param>
        public static void SetWindowStyle(DependencyObject obj, Style value)
        {
            obj.SetValue(WindowStyleProperty, value);
        }
    }
}