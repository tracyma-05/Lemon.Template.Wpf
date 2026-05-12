using Lemon.Template.Wpf.Commons;
using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public static class DialogExtensions
    {
        /// <summary>
        /// 询问窗口
        /// </summary>
        /// <param name="hostDialogService"></param>
        /// <param name="message">提示消息</param>
        /// <param name="IdentifierName">会话ID</param>
        /// <returns></returns>
        public static async Task<bool> Question(this IHostDialogService hostDialogService,
            string message,
            string IdentifierName = Constants.RootIdentifier)
        {
            return await Question(hostDialogService, "Are You Sure?", message, IdentifierName);
        }

        /// <summary>
        /// 询问窗口-指定标题
        /// </summary>
        /// <param name="hostDialogService"></param>
        /// <param name="title">标题</param>
        /// <param name="message">提示消息</param>
        /// <param name="IdentifierName">会话ID</param>
        /// <returns></returns>
        public static async Task<bool> Question(this IHostDialogService hostDialogService,
            string title,
            string message,
            string IdentifierName = Constants.RootIdentifier)
        {
            DialogParameters param = new DialogParameters();
            param.Add("Title", title);
            param.Add("Message", message);

            var dialogResult = await hostDialogService.ShowDialogAsync(Constants.HostMessageBox, param, IdentifierName);

            return dialogResult.Result == ButtonResult.OK;
        }

        /// <summary>
        /// 询问窗口
        /// </summary>
        /// <param name="dialogService"></param>
        /// <param name="title">标题</param>
        /// <param name="message">提示消息</param>
        /// <returns></returns>
        public static bool Question(this IDialogService dialogService, string title, string message)
        {
            if (string.IsNullOrWhiteSpace(title))
                title = "Are You Sure?";

            DialogParameters parameters = new DialogParameters();
            parameters.Add("Title", title);
            parameters.Add("Message", message);

            bool dialogResult = false;
            dialogService.ShowDialog(Constants.MessageBox, parameters, callback =>
            {
                dialogResult = callback.Result == ButtonResult.OK;
            });
            return dialogResult;
        }
    }
}