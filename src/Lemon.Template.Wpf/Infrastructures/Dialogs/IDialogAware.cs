using System;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IDialogAware
    {
        //
        // Summary:
        //     The title of the dialog that will show in the window title bar.
        string Title { get; }

        //
        // Summary:
        //     Instructs the Prism.Services.Dialogs.IDialogWindow to close the dialog.
        event Action<IDialogResult> RequestClose;

        //
        // Summary:
        //     Determines if the dialog can be closed.
        //
        // Returns:
        //     If true the dialog can be closed. If false the dialog will not close.
        bool CanCloseDialog();

        //
        // Summary:
        //     Called when the dialog is closed.
        void OnDialogClosed();

        //
        // Summary:
        //     Called when the dialog is opened.
        //
        // Parameters:
        //   parameters:
        //     The parameters passed to the dialog.
        void OnDialogOpened(IDialogParameters parameters);
    }
}