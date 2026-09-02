using System;

namespace MoAI.AiPlugin.Attributes;

/// <summary>
/// 标记一个 AI 插件，声明插件的唯一标识、名称与描述.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AiPluginAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AiPluginAttribute"/> class.
    /// </summary>
    /// <param name="key">插件的唯一标识（key），不区分大小写.</param>
    public AiPluginAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 插件的唯一标识（key）.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 插件描述/注释.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
