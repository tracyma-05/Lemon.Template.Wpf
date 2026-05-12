using Microsoft.Extensions.DependencyInjection;
using System;

namespace Lemon.Template.Wpf.Infrastructures.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class KeyedServiceAttribute : Attribute
    {
        public string Key { get; }
        public Type ServiceType { get; }
        public ServiceLifetime Lifetime { get; }

        public KeyedServiceAttribute(string key, Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            Key = key;
            ServiceType = serviceType;
            Lifetime = lifetime;
        }
    }
}