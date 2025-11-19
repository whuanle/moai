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
public sealed class NativePluginFieldConfigAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativePluginFieldConfigAttribute"/> class.
    /// </summary>
    /// <param name="key"></param>
    public NativePluginFieldConfigAttribute(string key)
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
}