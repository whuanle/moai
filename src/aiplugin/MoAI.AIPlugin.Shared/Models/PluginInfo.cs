using System;
using System.Collections.Generic;

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
    /// 面向 AI 的使用提示，供模型理解插件能力.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 面向管理员和用户的用途简介，供插件列表展示.
    /// </summary>
    public string HumanDescription { get; init; } = string.Empty;

    /// <summary>
    /// 插件实现类型.
    /// </summary>
    public Type PluginType { get; init; } = null!;

    /// <summary>
    /// 插件请求参数类型，多工具插件为 null（参数类型在 <see cref="Tools"/> 各工具上）.
    /// </summary>
    public Type? Request { get; init; }

    /// <summary>
    /// 插件响应结果类型，多工具插件为 null（响应类型在 <see cref="Tools"/> 各工具上）.
    /// </summary>
    public Type? Response { get; init; }

    /// <summary>
    /// 动态插件配置类型，静态插件为 null.
    /// </summary>
    public Type? ConfigType { get; init; }

    /// <summary>
    /// 是否为动态插件.
    /// </summary>
    public bool IsDynamic => ConfigType != null;

    /// <summary>
    /// 多工具插件（<see cref="Contracts.IDynamicPluginToolsRuntime{TConfig}"/>）的工具元数据列表；
    /// 单工具插件（RunAsync 约定）为空列表.
    /// </summary>
    public IReadOnlyList<PluginToolInfo> Tools { get; init; } = [];

    /// <summary>
    /// 是否为多工具插件.
    /// </summary>
    public bool IsMultiTool => Tools.Count > 0;

    /// <summary>
    /// 按工具 key 查找工具元数据.
    /// </summary>
    /// <param name="toolKey">工具 key，不区分大小写；空值返回 null.</param>
    /// <returns>返回 <see cref="PluginToolInfo"/>，不存在返回 null.</returns>
    public PluginToolInfo? FindTool(string? toolKey)
    {
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            return null;
        }

        foreach (var tool in Tools)
        {
            if (string.Equals(tool.Key, toolKey, StringComparison.OrdinalIgnoreCase))
            {
                return tool;
            }
        }

        return null;
    }
}
