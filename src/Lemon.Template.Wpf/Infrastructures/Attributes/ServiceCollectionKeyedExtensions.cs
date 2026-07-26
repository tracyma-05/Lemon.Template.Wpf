using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Lemon.Template.Wpf.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lemon.Template.Wpf.Infrastructures.Attributes
{
    public static class ServiceCollectionKeyedExtensions
    {
        public static IServiceCollection AddKeyedServicesFromAssembly(
            this IServiceCollection services,
            Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Implementation = t,
                    Attributes = t.GetCustomAttributes<KeyedServiceAttribute>(true)
                })
                .Where(x => x.Attributes.Any());

            foreach (var item in types)
            {
                foreach (var attr in item.Attributes)
                {
                    switch (attr.Lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            services.AddKeyedSingleton(attr.ServiceType, attr.Key, item.Implementation);
                            break;
                        case ServiceLifetime.Scoped:
                            services.AddKeyedScoped(attr.ServiceType, attr.Key, item.Implementation);
                            break;
                        default:
                            services.AddKeyedTransient(attr.ServiceType, attr.Key, item.Implementation);
                            break;
                    }
                }
            }

            return services;
        }

        public static IServiceCollection AddNavigationServiceFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Implementation = t,
                    Attributes = t.GetCustomAttributes<NavigationRegisterAttribute>(true)
                })
                .Where(x => x.Attributes.Any());

            foreach (var item in types)
            {
                foreach (var attr in item.Attributes)
                {
                    var (_, pageName, _) = ParseRegisterGroup(attr.RegisterGroup);
                    var key = $"{pageName}.{attr.Region}";

                    switch (attr.Lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            services.AddKeyedSingleton(attr.ServiceType, key, item.Implementation);
                            break;
                        case ServiceLifetime.Scoped:
                            services.AddKeyedScoped(attr.ServiceType, key, item.Implementation);
                            break;
                        default:
                            services.AddKeyedTransient(attr.ServiceType, key, item.Implementation);
                            break;
                    }                    
                }
            }

            return services;
        }

        public static void AddRouteServiceFromAssembly(IServiceProvider serviceProvider, Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Implementation = t,
                    Attributes = t.GetCustomAttributes<NavigationRegisterAttribute>(true)
                })
                .Where(x => x.Attributes.Any());

            var navigationService = serviceProvider.GetRequiredService<INavigationService>();
            var registerdGroups = new Dictionary<string, NavigationItem>();
            var registerdRoutes = new Dictionary<string, NavigationItem>();

            var registrationSequence = 0;
            var sortedRegistrations = types
                .SelectMany(item => item.Attributes, (item, attr) => (item.Implementation, attr, seq: registrationSequence++))
                .OrderBy(x => x.attr.DisplayOrder)
                .ThenBy(x => x.seq)
                .ToList();

            foreach (var (implementation, attr, _) in sortedRegistrations)
            {
                var (groupKey, pageName, isTopLevelPage) = ParseRegisterGroup(attr.RegisterGroup);

                navigationService.RegisterRoute(pageName, attr.ServiceType, attr.Region);

                var routeKey = attr.RegisterGroup;
                var icons = attr.Icons?.Split('/') ?? Array.Empty<string>();

                // 确保 Group 存在
                if (!registerdGroups.TryGetValue(groupKey, out var group))
                {
                    group = new NavigationItem()
                    {
                        Title = groupKey,
                        Icon = icons.FirstOrDefault() ?? string.Empty,
                        DisplayOrder = attr.DisplayOrder
                    };
                    registerdGroups[groupKey] = group;

                    if (!Constants.NavigationItems.Contains(group))
                        Constants.NavigationItems.Add(group);
                }
                else
                {
                    group.DisplayOrder = Math.Min(group.DisplayOrder, attr.DisplayOrder);
                }

                if (isTopLevelPage)
                {
                    // The group *is* the page: leaving Items empty is what makes the shell render it with
                    // NavigationChildlessItemTemplate instead of an expander.
                    if (group.Items.Count > 0)
                    {
                        Log.Warning(
                            "'{RegisterGroup}' registers a top-level page, but '{Group}' already has child pages; " +
                            "it will render as an expander and the page itself will be unreachable from the menu.",
                            attr.RegisterGroup,
                            groupKey);
                    }

                    registerdRoutes[routeKey] = group;
                    continue;
                }

                // 确保子菜单存在（会替换更新）
                var subMenu = EnsureSubMenu(
                    group,
                    pageName,
                    icons.LastOrDefault() ?? string.Empty,
                    attr.DisplayOrder);

                // 更新路由表
                registerdRoutes[routeKey] = subMenu;
            }

            ReorderNavigationRoots();
        }

        /// <summary>
        /// 解析注册键：<c>Group/Name</c> 表示分组下的子页面；单段 <c>Name</c> 表示没有子项的顶级页面
        /// （此时分组本身就是页面，如 <see cref="Constants.Home"/>）。
        /// </summary>
        private static (string Group, string PageName, bool IsTopLevelPage) ParseRegisterGroup(string? registerGroup)
        {
            if (string.IsNullOrWhiteSpace(registerGroup))
                throw new ArgumentException("RegisterGroup cannot be null or empty.", nameof(registerGroup));

            var parts = registerGroup.Split('/');
            return parts.Length switch
            {
                1 => (parts[0], parts[0], true),
                2 => (parts[0], parts[1], false),
                _ => throw new ArgumentException(
                    $"RegisterGroup format is incorrect ('{registerGroup}'). Expected 'Group/Name', " +
                    "or a single 'Name' for a top-level page with no children.",
                    nameof(registerGroup)),
            };
        }

        /// <summary>顶级菜单按组内最小 <see cref="NavigationItem.DisplayOrder"/> 排序。</summary>
        private static void ReorderNavigationRoots()
        {
            var roots = Constants.NavigationItems
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Title, StringComparer.Ordinal)
                .ToList();
            Constants.NavigationItems.Clear();
            foreach (var r in roots)
            {
                Constants.NavigationItems.Add(r);
            }
        }

        private static NavigationItem EnsureSubMenu(NavigationItem group, string title, string icon, int displayOrder)
        {
            // 查找已有的子菜单
            var existing = group.Items.FirstOrDefault(i => i.Title == title);

            if (existing != null)
            {
                // 替换图标，避免 UI 不更新
                existing.Icon = icon;
                existing.DisplayOrder = displayOrder;
                ReorderChildItems(group);
                return existing;
            }

            // 如果不存在，则新建并加入
            var subMenu = new NavigationItem
            {
                Title = title,
                Icon = icon,
                DisplayOrder = displayOrder
            };

            group.Items.Add(subMenu);
            ReorderChildItems(group);
            return subMenu;
        }

        private static void ReorderChildItems(NavigationItem group)
        {
            var ordered = group.Items
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.Title, StringComparer.Ordinal)
                .ToList();
            group.Items.Clear();
            foreach (var x in ordered)
            {
                group.Items.Add(x);
            }
        }
    }
}