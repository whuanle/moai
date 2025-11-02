using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Plugin.Attributes;

/// <summary>
/// 插件的名称属性，只有系统插件使用，作为插件的唯一表示.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginNameAttribute"/> class.
    /// </summary>
    /// <param name="name"></param>
    public PluginNameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 插件的唯一标识.
    /// </summary>
    public string Name { get; }
}
