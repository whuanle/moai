using MoAI.Plugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Plugin.Attributes;

/// <summary>
/// 内置插件.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InternalPluginAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InternalPluginAttribute"/> class.
    /// </summary>
    /// <param name="key"></param>
    public InternalPluginAttribute(string key)
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
    public InternalPluginClassify Classify { get; init; }

    /// <summary>
    /// 是否需要配置，需要配置的插件都需要实例化并存储到数据库.
    /// </summary>
    public bool RequiredConfiguration { get; init; } = true;
}