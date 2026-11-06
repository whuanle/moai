using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件元数据补充与解析工具.
/// </summary>
public static class PluginTypeHelper
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
        var definition = pluginInterface.GetGenericTypeDefinition();
        if (definition == typeof(Contracts.IDynamicPluginToolsRuntime<>))
        {
            // 多工具动态插件：参数/响应类型在工具方法上，插件级 Request/Response 置空
            var tools = DiscoverTools(type);
            if (tools.Count == 0)
            {
                throw new InvalidOperationException($"多工具插件 {type.FullName} 未声明任何 [AiPluginTool] 工具方法");
            }

            return new PluginInfo
            {
                Key = attribute.Key,
                Name = attribute.Name,
                Description = attribute.Description,
                PluginType = type,
                Request = null,
                Response = null,
                ConfigType = args[0],
                Tools = tools,
            };
        }

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
    /// 扫描多工具插件类型的公开实例方法，收集 <see cref="Attributes.AiPluginToolAttribute"/> 标记的工具元数据.
    /// </summary>
    /// <param name="pluginType">插件类型.</param>
    /// <returns>工具元数据列表（按声明顺序）.</returns>
    public static List<PluginToolInfo> DiscoverTools(Type pluginType)
    {
        var tools = new List<PluginToolInfo>();
        foreach (var method in pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance).OrderBy(m => m.MetadataToken))
        {
            var attr = method.GetCustomAttribute<Attributes.AiPluginToolAttribute>();
            if (attr == null)
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (method.IsAbstract
                || method.IsGenericMethodDefinition
                || parameters.Length != 2
                || parameters[1].ParameterType != typeof(CancellationToken)
                || !typeof(Task).IsAssignableFrom(method.ReturnType)
                || method.ReturnType == typeof(Task))
            {
                throw new InvalidOperationException(
                    $"插件 {pluginType.FullName} 的工具方法 {method.Name} 签名不符合约定：须为 Task<TResponse> XxxAsync(TRequest request, CancellationToken cancellationToken)");
            }

            var requestType = parameters[0].ParameterType;
            var responseType = method.ReturnType.GetGenericArguments()[0];

            // 参数示例：特性内联优先，缺省回落请求类型上的 static GetParamsExampleValue()
            var example = attr.ParamsExample;
            if (string.IsNullOrWhiteSpace(example))
            {
                example = GetStaticExample(requestType, "GetParamsExampleValue");
            }

            tools.Add(new PluginToolInfo
            {
                Key = attr.Key,
                Title = attr.Title,
                Description = attr.Description,
                Request = requestType,
                Response = responseType,
                Method = method,
                ParamsExample = example,
            });
        }

        var duplicated = tools.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicated != null)
        {
            throw new InvalidOperationException($"插件 {pluginType.FullName} 存在重复的工具 key：{duplicated.Key}");
        }

        return tools;
    }

    /// <summary>
    /// 查找类型实现的具体插件运行时接口（静态/动态/多工具动态）.
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

            if (definition == typeof(Contracts.IDynamicPluginToolsRuntime<>))
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

    /// <summary>
    /// 组装插件的工具清单响应项：多工具插件展开为全部工具，单工具插件固定一个 key=run 的项.
    /// </summary>
    /// <param name="plugin">插件元数据.</param>
    /// <returns>工具项列表.</returns>
    public static List<Queries.Responses.QueryPluginToolItem> BuildToolItems(PluginInfo plugin)
    {
        if (plugin.IsMultiTool)
        {
            return plugin.Tools.Select(t => new Queries.Responses.QueryPluginToolItem
            {
                Key = t.Key,
                Title = t.Title,
                Description = t.Description,
                ParamsExample = t.ParamsExample,
            }).ToList();
        }

        return
        [
            new Queries.Responses.QueryPluginToolItem
            {
                Key = "run",
                Title = plugin.Name,
                Description = plugin.Description,
                ParamsExample = GetStaticExample(plugin.PluginType, "GetParamsExampleValue"),
            },
        ];
    }

    /// <summary>
    /// 反射生成响应类型的字段 schema（顶层公开可读属性），用于设计器自动填充输出参数.
    /// 字段名优先取 <see cref="JsonPropertyNameAttribute"/>，否则 camelCase 属性名；描述取 <see cref="DescriptionAttribute"/>.
    /// </summary>
    /// <param name="responseType">插件响应类型，可为空.</param>
    /// <returns>字段 schema 列表；无响应类型或无公开属性返回空列表.</returns>
    public static List<PluginFieldSchema> GetResponseSchema(Type? responseType)
    {
        var result = new List<PluginFieldSchema>();
        if (responseType == null || responseType.IsPrimitive || responseType == typeof(string))
        {
            return result;
        }

        foreach (var property in responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? ToCamelCase(property.Name);
            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;

            result.Add(new PluginFieldSchema
            {
                Name = jsonName,
                FieldType = MapFieldType(property.PropertyType),
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
            });
        }

        return result;
    }

    private static string ToCamelCase(string name)
    {
        return string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string MapFieldType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset))
        {
            return "string";
        }

        if (t == typeof(bool))
        {
            return "boolean";
        }

        if (t.IsPrimitive || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) || t == typeof(decimal))
        {
            if (t == typeof(object))
            {
                return "dynamic";
            }

            return "number";
        }

        if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() != typeof(Nullable<>) && typeof(System.Collections.IEnumerable).IsAssignableFrom(t)))
        {
            return "array";
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
        {
            return "array";
        }

        return "object";
    }
}
