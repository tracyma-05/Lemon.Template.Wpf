using System;
using System.ComponentModel;
using System.Windows;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IDialogWindow
    {
        //
        // Summary:
        //     Dialog content.
        object? Content { get; set; }

        //
        // Summary:
        //     The window's owner.
        Window? Owner { get; set; }

        //
        // Summary:
        //     The data context of the window.
        //
        // Remarks:
        //     The data context must implement Prism.Services.Dialogs.IDialogAware.
        object? DataContext { get; set; }

        //
        // Summary:
        //     The result of the dialog. Null until the dialog closes.
        IDialogResult? Result { get; set; }

        //
        // Summary:
        //     The window style.
        Style? Style { get; set; }

        //
        // Summary:
        //     Called when the window is loaded.
        event RoutedEventHandler Loaded;

        //
        // Summary:
        //     Called when the window is closed.
        event EventHandler Closed;

        //
        // Summary:
        //     Called when the window is closing.
        event CancelEventHandler Closing;

        //
        // Summary:
        //     Close the window.
        void Close();

        //
        // Summary:
        //     Show a non-modal dialog.
        void Show();

        //
        // Summary:
        //     Show a modal dialog.
        bool? ShowDialog();
    }
}