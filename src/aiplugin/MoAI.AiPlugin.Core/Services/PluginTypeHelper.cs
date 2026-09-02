using System;
using System.Collections.Generic;
using System.Reflection;
using MoAI.AiPlugin.Models;

namespace MoAI.AiPlugin.Services;

/// <summary>
/// 插件元数据补充与解析工具.
/// </summary>
internal static class PluginTypeHelper
{
    /// <summary>
    /// 尝试从类型抽取插件元数据；非插件类型返回 null.
    /// </summary>
    /// <param name="type">候选类型.</param>
    /// <returns>返回 <see cref="PluginInfo"/>，非插件返回 null.</returns>
    public static PluginInfo? TryGetPluginInfo(Type type)
    {
        if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
        {
            return null;
        }

        var pluginInterface = FindPluginInterface(type);
        if (pluginInterface == null)
        {
            return null;
        }

        var attribute = type.GetCustomAttribute<Attributes.AiPluginAttribute>();
        if (attribute == null)
        {
            return null;
        }

        var args = pluginInterface.GetGenericArguments();
        return new PluginInfo
        {
            Key = attribute.Key,
            Name = attribute.Name,
            Description = attribute.Description,
            PluginType = type,
            Request = args[0],
            Response = args[1],
            ConfigType = args.Length > 2 ? args[2] : null,
        };
    }

    /// <summary>
    /// 查找类型实现的具体插件运行时接口（静态或动态）.
    /// </summary>
    /// <param name="type">候选类型.</param>
    /// <returns>返回插件运行时接口，否则 null.</returns>
    public static Type? FindPluginInterface(Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(Contracts.IStaticPluginRuntime<,>))
            {
                return iface;
            }

            if (definition == typeof(Contracts.IDynamicPluginRuntime<,,>))
            {
                return iface;
            }
        }

        return null;
    }

    /// <summary>
    /// 读取插件的静态示例方法返回值为字符串.
    /// </summary>
    /// <param name="pluginType">插件类型.</param>
    /// <param name="methodName">静态方法名.</param>
    /// <returns>示例 JSON 字符串，方法不存在返回 null.</returns>
    public static string? GetStaticExample(Type pluginType, string methodName)
    {
        var method = pluginType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return null;
        }

        try
        {
            return method.Invoke(null, null) as string;
        }
#pragma warning disable CA1031 // 示例读取失败不阻塞发现流程，返回 null 即可
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
