namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public class DialogResult : IDialogResult
    {
        //
        // Summary:
        //     The parameters from the dialog.
        public IDialogParameters Parameters { get; private set; } = new DialogParameters();


        //
        // Summary:
        //     The result of the dialog.
        public ButtonResult Result { get; private set; }

        //
        // Summary:
        //     Initializes a new instance of the Prism.Services.Dialogs.DialogResult class.
        public DialogResult()
        {
        }

        //
        // Summary:
        //     Initializes a new instance of the Prism.Services.Dialogs.DialogResult class.
        //
        //
        // Parameters:
        //   result:
        //     The result of the dialog.
        public DialogResult(ButtonResult result)
        {
            Result = result;
        }

        //
        // Summary:
        //     Initializes a new instance of the Prism.Services.Dialogs.DialogResult class.
        //
        //
        // Parameters:
        //   result:
        //     The result of the dialog.
        //
        //   parameters:
        //     The parameters from the dialog.
        public DialogResult(ButtonResult result, IDialogParameters parameters)
        {
            Result = result;
            Parameters = parameters;
        }
    }
}