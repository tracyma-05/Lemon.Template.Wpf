using System;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IDialogService
    {
        void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback);

        void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName);

        void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback);

        void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName);
    }
}