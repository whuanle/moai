#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins;
using MoAI.Plugin.Queries.Responses;
using System.ComponentModel;
using System.Reflection;

namespace MoAI.Plugin;

public static class InternalPluginFactory
{
    private static readonly IReadOnlyDictionary<Type, BuildPluginItem> _plugins;
    private static readonly IReadOnlyDictionary<string, BuildPluginItem> _pluginstrs;

    /// <summary>
    /// 插件列表.
    /// </summary>
    public static IReadOnlyDictionary<string, BuildPluginItem> Plugins => _pluginstrs;

    static InternalPluginFactory()
    {
        var types = typeof(PluginBuildInModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(IInternalPluginRuntime)))
            .ToArray();

        Dictionary<Type, BuildPluginItem> plugins = new Dictionary<Type, BuildPluginItem>(types.Length);
        Dictionary<string, BuildPluginItem> pluginstrs = new Dictionary<string, BuildPluginItem>(types.Length);

        foreach (var item in types)
        {
            var key = item.GetCustomAttribute<PluginKeyAttribute>();
            var name = item.GetCustomAttribute<PluginNameAttribute>();
            var classify = item.GetCustomAttribute<PluginClassifyAttribute>();

            DescriptionAttribute? description = item.GetCustomAttribute<DescriptionAttribute>();

            if (key == null || name == null || description == null)
            {
                continue;
            }

            var plugin = new BuildPluginItem
            {
                PluginKey = key.Key,
                PluginName = name.Name,
                Description = description.Description,
                Type = item,
                Classify = classify?.Key ?? InternalPluginClassify.Others
            };

            plugins.Add(item, plugin);
            pluginstrs.Add(key.Key, plugin);
        }

        _plugins = plugins;
        _pluginstrs = pluginstrs;
    }
}