using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Plugin.Attributes;

/// <summary>
/// 只有系统插件使用，创建分类.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginClassifyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginKeyAttribute"/> class.
    /// </summary>
    /// <param name="key"></param>
    public PluginClassifyAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 插件的唯一标识.
    /// </summary>
    public string Key { get; }
}
