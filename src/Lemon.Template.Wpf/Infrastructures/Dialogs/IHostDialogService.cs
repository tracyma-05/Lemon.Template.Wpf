using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IHostDialogService : IDialogService
    {
        Task<IDialogResult> ShowDialogAsync(
            string name,
            IDialogParameters? parameters = null,
            string IdentifierName = "Root");

        /// <summary>Shows a keyed dialog in a modal window and returns its result.</summary>
        IDialogResult ShowWindow(string name, IDialogParameters? parameters = null);

        void Close(string IdentifierName, DialogResult dialogResult);
    }
}