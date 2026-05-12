namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public class DialogParameters : ParametersBase, IDialogParameters
    {
        //
        // Summary:
        //     Initializes a new instance of the Prism.Services.Dialogs.DialogParameters class.
        public DialogParameters()
        {
        }

        //
        // Summary:
        //     Constructs a list of parameters.
        //
        // Parameters:
        //   query:
        //     Query string to be parsed.
        public DialogParameters(string query)
            : base(query)
        {
        }
    }
}