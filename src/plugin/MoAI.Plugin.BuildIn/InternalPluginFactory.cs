#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MoAI.Plugin.Attributes;
using MoAI.Plugin.Plugins;
using MoAI.Plugin.Queries.Responses;
using System.Reflection;

namespace MoAI.Plugin;

/// <summary>
/// 插件工厂.
/// </summary>
public static class InternalPluginFactory
{
    private static readonly IReadOnlyCollection<InternalTemplatePlugin> _plugins;

    /// <summary>
    /// 插件列表.
    /// </summary>
    public static IReadOnlyCollection<InternalTemplatePlugin> Plugins => _plugins;

    static InternalPluginFactory()
    {
        var types = typeof(PluginBuildInModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(IInternalPluginRuntime)))
            .ToArray();

        List<InternalTemplatePlugin> plugins = new List<InternalTemplatePlugin>();

        foreach (var item in types)
        {
            var internalPluginAttribute = item.GetCustomAttribute<InternalPluginAttribute>();

            if (internalPluginAttribute == null)
            {
                continue;
            }

            var plugin = new InternalTemplatePlugin
            {
                Key = internalPluginAttribute.Key,
                Description = internalPluginAttribute.Description,
                Name = internalPluginAttribute.Name,
                Type = item,
                Classify = internalPluginAttribute.Classify,
                RequiredConfiguration = internalPluginAttribute.RequiredConfiguration,
            };

            plugins.Add(plugin);
        }

        _plugins = plugins;
    }
}