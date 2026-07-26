using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Infrastructures.Localization;
using System.Collections.ObjectModel;

namespace Lemon.Template.Wpf.Models
{
    public partial class NavigationItem : ObservableObject
    {
        public NavigationItem()
        {
            Items = new ObservableCollection<NavigationItem>();

            // Menu items live for the lifetime of the app, so this subscription is never detached.
            LocalizationService.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(DisplayTitle));
        }

        public NavigationItem(
            string title,
            string icon,
            string? pageViewName,
            string? requiredPermissionName,
            ObservableCollection<NavigationItem>? items = null)
            : this()
        {
            Icon = icon;
            Title = title;
            PageViewName = pageViewName;
            RequiredPermissionName = requiredPermissionName;
            Items = items ?? new ObservableCollection<NavigationItem>();
        }

        /// <summary>
        /// Route key, also used as the resource lookup key. Not shown directly in the UI — bind to
        /// <see cref="DisplayTitle"/> so the label follows the selected language.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayTitle))]
        private string _title = string.Empty;

        /// <summary>Localized label for <see cref="Title"/>, falling back to the route key itself.</summary>
        public string DisplayTitle => LocalizationService.Instance.GetMenuTitle(Title);

        [ObservableProperty]
        private string _icon = string.Empty;

        /// <summary>与 <see cref="NavigationRegisterAttribute.DisplayOrder"/> 对应，用于菜单排序（越小越靠前）。</summary>
        [ObservableProperty]
        private int _displayOrder;

        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// 分组是否展开。由 <see cref="Infrastructures.Navigations.IMenuNavigator"/> 在跳转到子页面时置为
        /// <c>true</c>，这样从首页快捷入口进入的页面在菜单中也是可见的。
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded;

        /// <summary>
        /// 页面名称
        /// </summary>
        public string? PageViewName { get; set; }

        /// <summary>
        /// 导航参数
        /// </summary>
        public object? NavigationParameter { get; set; }

        /// <summary>
        /// 权限名
        /// </summary>
        public string? RequiredPermissionName { get; set; }

        [ObservableProperty]
        private ObservableCollection<NavigationItem> _items;
    }
}