using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace Lemon.Template.Avalonia.Infrastructures.Dialogs
{
    public interface IDialogWindow
    {
        /// <summary>Dialog content.</summary>
        object? Content { get; set; }

        /// <summary>
        /// The data context of the window. Must implement <see cref="IDialogAware"/>.
        /// </summary>
        object? DataContext { get; set; }

        /// <summary>The result of the dialog. Null until the dialog closes.</summary>
        IDialogResult? Result { get; set; }

        /// <summary>Called when the window is loaded.</summary>
        event EventHandler<RoutedEventArgs>? Loaded;

        /// <summary>Called when the window is closed.</summary>
        event EventHandler? Closed;

        /// <summary>Called when the window is closing; set <c>Cancel</c> to keep it open.</summary>
        event EventHandler<WindowClosingEventArgs>? Closing;

        /// <summary>Close the window.</summary>
        void Close();

        /// <summary>Show a non-modal dialog.</summary>
        void Show();

        /// <summary>
        /// Show a modal dialog. Unlike WPF, Avalonia cannot block the caller: the task completes when the
        /// window closes.
        /// </summary>
        Task ShowDialog(Window owner);
    }
}
