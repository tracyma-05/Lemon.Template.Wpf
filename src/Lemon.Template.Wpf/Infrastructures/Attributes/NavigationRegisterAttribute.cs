using Microsoft.Extensions.DependencyInjection;

namespace Lemon.Template.Wpf.Infrastructures.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NavigationRegisterAttribute : Attribute
    {
        public string RegisterGroup { get; }
        public Type ServiceType { get; }
        public string Region { get; }
        public string Icons { get; set; }

        public ServiceLifetime Lifetime { get; }

        /// <summary>
        /// 菜单排序：数值越小越靠前；同级未指定时均为 0，顺序与程序集扫描的稳定顺序一致。
        /// 用法：<c>[NavigationRegister(..., DisplayOrder = 10)]</c>。
        /// </summary>
        public int DisplayOrder { get; set; }

        public NavigationRegisterAttribute(string registerGroup, string region, Type serviceType, string icons, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            RegisterGroup = registerGroup;
            ServiceType = serviceType;
            Region = region;
            Icons = icons;
            Lifetime = lifetime;
        }
    }
}