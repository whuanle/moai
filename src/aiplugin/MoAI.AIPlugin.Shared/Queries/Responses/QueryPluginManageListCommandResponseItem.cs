using System;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件管理列表响应项.
/// </summary>
public class QueryPluginManageListCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 插件记录 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 类型：mcp|openapi|native|tool（对应 PluginEntity.Type）.
    /// </summary>
    public int Type { get; init; }

    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 分类名称，分类不存在或为 0 时为 null.
    /// </summary>
    public string? ClassifyName { get; init; }

    /// <summary>
    /// 插件种类：custom|dynamic|static.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// 是否为系统插件.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 是否公开访问.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 静态插件 key，仅静态插件有；用于前端编辑写回定位.
    /// </summary>
    public string? PluginKey { get; init; }

    /// <summary>
    /// 静态插件请求参数示例 JSON，仅静态插件有；抽屉 Monaco 初始值.
    /// </summary>
    public string? ParamsExample { get; init; }

    /// <summary>
    /// 动态插件模板 key，仅动态插件实例有.
    /// </summary>
    public string? TempleteKey { get; init; }

    /// <summary>
    /// 动态插件实例配置 JSON，仅动态插件实例有.
    /// </summary>
    public string? Config { get; init; }

    /// <summary>
    /// 动态插件模板配置示例 JSON，仅动态插件实例有；创建实例时的 Monaco 初始值.
    /// </summary>
    public string? ConfigExample { get; init; }
}
