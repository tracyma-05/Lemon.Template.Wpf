using System;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    internal static class IDialogWindowExtensions
    {
        /// <summary>
        /// Get the <see cref="IDialogAware"/> ViewModel from a <see cref="IDialogWindow"/>.
        /// </summary>
        /// <param name="dialogWindow"><see cref="IDialogWindow"/> to get ViewModel from.</param>
        /// <returns>ViewModel as a <see cref="IDialogAware"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// The window has no DataContext, or it does not implement <see cref="IDialogAware"/>.
        /// </exception>
        internal static IDialogAware GetDialogViewModel(this IDialogWindow dialogWindow)
        {
            return dialogWindow.DataContext as IDialogAware
                   ?? throw new InvalidOperationException(
                       $"A dialog window's DataContext must implement {nameof(IDialogAware)}; " +
                       $"found '{dialogWindow.DataContext?.GetType().FullName ?? "null"}'.");
        }
    }
}