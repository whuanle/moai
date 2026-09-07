using System;
using MoAI.AIPlugin.Models;

namespace MoAI.TeamPlugin.Queries.Responses;

/// <summary>
/// 团队可用插件项：本团队拥有的插件（自定义/动态）或本团队可用的系统插件.
/// </summary>
public class TeamPluginItem
{
    /// <summary>
    /// 插件记录 id；系统内存插件的静态注册插件为 Guid.Empty.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// 插件名称/Key.
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// 插件标题（展示名称）.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 插件描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 插件类型：mcp|openapi|native|tool.
    /// </summary>
    public PluginType Type { get; set; }

    /// <summary>
    /// 插件种类：custom|dynamic|static.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 是否为本团队自己创建的插件（false=系统插件）.
    /// </summary>
    public bool IsTeamOwned { get; set; }

    /// <summary>
    /// 是否为系统插件（true=系统插件）.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; set; }

    /// <summary>
    /// 分类名称，分类不存在或为 0 时为 null.
    /// </summary>
    public string? ClassifyName { get; init; }

    /// <summary>
    /// 使用量计数.
    /// </summary>
    public int Counter { get; set; }

    /// <summary>
    /// 动态插件实例 key（仅动态插件实例有）.
    /// </summary>
    public string? InstanceKey { get; set; }

    /// <summary>
    /// 动态插件模板 key，仅动态插件实例有.
    /// </summary>
    public string? TempleteKey { get; init; }

    /// <summary>
    /// 动态插件实例配置 JSON，仅动态插件实例有.
    /// </summary>
    public string? Config { get; init; }

    /// <summary>
    /// 动态插件模板配置示例 JSON，仅动态插件实例有.
    /// </summary>
    public string? ConfigExample { get; init; }

    /// <summary>
    /// 自定义插件服务器地址（仅 custom 有）.
    /// </summary>
    public string? Server { get; init; }

    /// <summary>
    /// 静态插件 key（仅静态插件有）.
    /// </summary>
    public string? PluginKey { get; init; }

    /// <summary>
    /// 静态插件请求参数示例 JSON（仅静态插件有）.
    /// </summary>
    public string? ParamsExample { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 创建人 id.
    /// </summary>
    public long CreateUserId { get; set; }
}
