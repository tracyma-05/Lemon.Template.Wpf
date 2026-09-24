using System;
using System.Threading.Tasks;

namespace Lemon.Template.Avalonia.Infrastructures.Dialogs
{
    public interface IDialogService
    {
        /// <summary>Shows a non-modal dialog window; <paramref name="callback"/> runs when it closes.</summary>
        void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback);

        /// <inheritdoc cref="Show(string, IDialogParameters?, Action{IDialogResult}?)"/>
        /// <param name="windowName">Key of the hosting <see cref="IDialogWindow"/>; the default window when null.</param>
        void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback, string? windowName);

        /// <summary>
        /// Shows a keyed dialog in a modal window and completes with its result once it closes.
        /// </summary>
        /// <remarks>
        /// Replaces the WPF template's synchronous <c>ShowDialog</c>/<c>ShowWindow</c>: an Avalonia modal
        /// window cannot block the calling thread, so the result is only available by awaiting.
        /// </remarks>
        Task<IDialogResult> ShowWindowAsync(string name, IDialogParameters? parameters = null, string? windowName = null);
    }
}
