using System.Threading.Tasks;

namespace Lemon.Template.Avalonia.Infrastructures.Dialogs
{
    public interface IHostDialogService : IDialogService
    {
        /// <summary>Shows a keyed dialog inside the <c>DialogHost</c> named <paramref name="IdentifierName"/>.</summary>
        Task<IDialogResult> ShowDialogAsync(
            string name,
            IDialogParameters? parameters = null,
            string IdentifierName = "Root");

        void Close(string IdentifierName, DialogResult dialogResult);
    }
}
