using MoAI.AI.Models;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Queries.Responses;

/// <summary>
/// 查询插件描述信息.
/// </summary>
public class QueryCustomPluginDetailCommandResponse : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 服务器地址.
    /// </summary>
    public string Server { get; set; } = default!;

    /// <summary>
    /// Header 头部信息.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Header { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// Query 字典.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Query { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// system|mcp|openapi.
    /// </summary>
    public PluginType Type { get; set; }

    /// <summary>
    /// 文件id.
    /// </summary>
    public int OpenapiFileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string? OpenapiFileName { get; set; } = default!;

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}