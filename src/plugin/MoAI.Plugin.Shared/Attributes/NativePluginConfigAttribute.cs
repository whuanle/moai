#pragma warning disable CA1019 // 定义属性参数的访问器

using MoAI.Plugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Plugin.Attributes;

/// <summary>
/// 内置插件，在原生插件以及工具插件都可以使用.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NativePluginConfigAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativePluginConfigAttribute"/> class.
    /// </summary>
    /// <param name="key"></param>
    public NativePluginConfigAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 插件的唯一标识.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 分类.
    /// </summary>
    public NativePluginClassify Classify { get; init; }

    /// <summary>
    /// 配置模型类，只有 tool 不需要配置.
    /// </summary>
    public Type? ConfigType { get; init; }
}