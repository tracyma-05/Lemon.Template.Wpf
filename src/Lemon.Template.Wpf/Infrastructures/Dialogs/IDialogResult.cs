namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IDialogResult
    {
        //
        // Summary:
        //     The parameters from the dialog.
        IDialogParameters Parameters { get; }

        //
        // Summary:
        //     The result of the dialog.
        ButtonResult Result { get; }
    }
}