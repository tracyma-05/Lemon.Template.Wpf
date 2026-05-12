using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Lemon.Template.Wpf.Models
{
    [ObservableObject]
    public partial class NavigationItem
    {
        public NavigationItem()
        {
            Items = new ObservableCollection<NavigationItem>();
        }

        public NavigationItem(
            string title,
            string icon,
            string? pageViewName,
            string? requiredPermissionName,
            ObservableCollection<NavigationItem> items = null)
        {
            Icon = icon;
            Title = title;
            PageViewName = pageViewName;
            RequiredPermissionName = requiredPermissionName;
            Items = items ?? new ObservableCollection<NavigationItem>();
        }

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _icon;

        /// <summary>与 <see cref="NavigationRegisterAttribute.DisplayOrder"/> 对应，用于菜单排序（越小越靠前）。</summary>
        [ObservableProperty]
        private int _displayOrder;

        [ObservableProperty]
        private bool _isSelected;

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