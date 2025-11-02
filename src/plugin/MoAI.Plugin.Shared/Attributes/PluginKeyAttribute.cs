using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Plugin.Attributes;

/// <summary>
/// 只有系统插件使用，作为插件的唯一表示.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginKeyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginKeyAttribute"/> class.
    /// </summary>
    /// <param name="key"></param>
    public PluginKeyAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 插件的唯一标识.
    /// </summary>
    public string Key { get; }
}