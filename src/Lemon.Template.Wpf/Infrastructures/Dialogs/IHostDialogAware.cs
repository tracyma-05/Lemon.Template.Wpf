using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IHostDialogAware
    {
        /// <summary>
        /// DialogHost顶级节点
        /// </summary>
        string IdentifierName { get; set; }

        /// <summary>
        /// 页面初始化前传递参数事件
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        void OnDialogOpened(IDialogParameters parameters);

        /// <summary>
        /// 确认
        /// </summary>
        Task Save();

        /// <summary>
        /// 取消
        /// </summary>
        void Cancel();
    }
}