using System;

namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件元数据信息，由执行引擎在扫描插件程序集时生成.
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// 插件的唯一标识（key）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 插件描述/注释.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 插件实现类型.
    /// </summary>
    public Type PluginType { get; init; } = null!;

    /// <summary>
    /// 插件请求参数类型.
    /// </summary>
    public Type Request { get; init; } = null!;

    /// <summary>
    /// 插件响应结果类型.
    /// </summary>
    public Type Response { get; init; } = null!;

    /// <summary>
    /// 动态插件配置类型，静态插件为 null.
    /// </summary>
    public Type? ConfigType { get; init; }

    /// <summary>
    /// 是否为动态插件.
    /// </summary>
    public bool IsDynamic => ConfigType != null;
}
