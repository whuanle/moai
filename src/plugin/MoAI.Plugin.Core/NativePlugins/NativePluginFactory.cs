#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using Maomi;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries.Responses;
using MoAI.Plugin.Plugins;
using MoAI.Plugin.Plugins.BoCha.AiSearch;
using System.Diagnostics;
using System.Reflection;

namespace MoAI.Plugin;

/// <summary>
/// 插件工厂.
/// </summary>
[InjectOnSingleton]
public class NativePluginFactory : INativePluginFactory
{
    private static readonly Dictionary<string, NativePluginTemplateInfo> _plugins;
    private static readonly IReadOnlyCollection<NativePluginTemplateInfo> _pluginList;

    static NativePluginFactory()
    {
        var nativePluginTypes = typeof(NativePluginModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(INativePluginRuntime)))
            .ToArray();

        var toolPluginTypes = typeof(ToolPluginModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(IToolPluginRuntime)))
            .ToArray();

        var plugins = new Dictionary<string, NativePluginTemplateInfo>();

        foreach (var item in nativePluginTypes)
        {
            var internalPluginAttribute = item.GetCustomAttribute<NativePluginConfigAttribute>();

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
                PluginType = PluginType.NativePlugin,
                ConfigType = internalPluginAttribute.ConfigType,
                FieldTemplates = GetFieldTemplates(internalPluginAttribute.ConfigType!),
                ExampleValue = GetExampleValue(item)
            };

            plugins.Add(internalPluginAttribute.Key, plugin);
        }

        foreach (var item in toolPluginTypes)
        {
            var internalPluginAttribute = item.GetCustomAttribute<NativePluginConfigAttribute>();

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
                PluginType = PluginType.ToolPlugin,
                ConfigType = internalPluginAttribute.ConfigType,
                ExampleValue = GetExampleValue(item)
            };

            plugins.Add(internalPluginAttribute.Key, plugin);
        }

        if (plugins.DistinctBy(x => x.Key).Count() != plugins.Count)
        {
            Debug.Assert(false, "存在重复的内置插件Key，请检查插件定义");
        }

        _plugins = plugins;
        _pluginList = plugins.Values.ToArray();
    }

    /// <inheritdoc/>
    public NativePluginTemplateInfo? GetPluginByKey(string key)
    {
        _plugins.TryGetValue(key, out var plugin);
        return plugin;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<NativePluginTemplateInfo> GetPlugins()
    {
        return _pluginList;
    }

    private static List<NativePluginConfigFieldTemplate> GetFieldTemplates(Type configType)
    {
        List<NativePluginConfigFieldTemplate> templates = new();
        var properties = configType.GetProperties();

        foreach (var item in properties)
        {
            var nativePluginConfigFieldAttribute = item.GetCustomAttribute<NativePluginConfigFieldAttribute>();
            if (nativePluginConfigFieldAttribute != null)
            {
                templates.Add(new NativePluginConfigFieldTemplate
                {
                    Key = item.Name,
                    Description = nativePluginConfigFieldAttribute.Description,
                    IsRequired = nativePluginConfigFieldAttribute.IsRequired,
                    FieldType = nativePluginConfigFieldAttribute.FieldType,
                    ExampleValue = nativePluginConfigFieldAttribute.ExampleValue,
                });
            }
        }

        return templates;
    }

    private static string GetExampleValue(Type type)
    {
        var methodInfo = type.GetMethod(nameof(IToolPluginRuntime.GetParamsExampleValue), BindingFlags.Static | BindingFlags.Public);
        var result = methodInfo!.Invoke(null, null);
        return result!.ToString()!;
    }
}