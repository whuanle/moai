#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MoAI.Infra.Exceptions;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.NativePlugins.Queries.Responses;
using MoAI.Plugin.Plugins;
using System.Diagnostics;
using System.Reflection;

namespace MoAI.Plugin;

/// <summary>
/// 插件工厂.
/// </summary>
public static class NativePluginFactory
{
    private static readonly IReadOnlyCollection<NativePluginTemplateInfo> _plugins;

    /// <summary>
    /// 插件列表.
    /// </summary>
    public static IReadOnlyCollection<NativePluginTemplateInfo> Plugins => _plugins;

    static NativePluginFactory()
    {
        var nativePluginTypes = typeof(NativePluginModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(INativePluginRuntime)))
            .ToArray();

        var toolPluginTypes = typeof(ToolPluginModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(IToolPluginRuntime)))
            .ToArray();

        var plugins = new SortedList<string, NativePluginTemplateInfo>();

        foreach (var item in nativePluginTypes)
        {
            var internalPluginAttribute = item.GetCustomAttribute<NativePluginFieldConfigAttribute>();

            if (internalPluginAttribute == null)
            {
                continue;
            }

            var plugin = new NativePluginTemplateInfo
            {
                Key = internalPluginAttribute.Key,
                Description = internalPluginAttribute.Description,
                Name = internalPluginAttribute.Name,
                Type = item,
                Classify = internalPluginAttribute.Classify,
                IsTool = false,
            };

            plugins.Add(internalPluginAttribute.Key, plugin);
        }

        foreach (var item in toolPluginTypes)
        {
            var internalPluginAttribute = item.GetCustomAttribute<NativePluginFieldConfigAttribute>();

            if (internalPluginAttribute == null)
            {
                continue;
            }

            var plugin = new NativePluginTemplateInfo
            {
                Key = internalPluginAttribute.Key,
                Description = internalPluginAttribute.Description,
                Name = internalPluginAttribute.Name,
                Type = item,
                Classify = internalPluginAttribute.Classify,
                IsTool = true,
            };

            plugins.Add(internalPluginAttribute.Key, plugin);
        }

        if (plugins.DistinctBy(x => x.Key).Count() != plugins.Count)
        {
            Debug.Assert(false, "存在重复的内置插件Key，请检查插件定义");
        }

        _plugins = plugins.Values.ToArray();
    }
}