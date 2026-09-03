using System;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件基础信息项.
/// </summary>
public class PluginBaseInfoItem : AuditsInfo
{
    /// <summary>
    /// 插件记录 id.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 服务器地址.
    /// </summary>
    public string Server { get; set; } = default!;

    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType Type { get; set; }

    /// <summary>
    /// 文件 id.
    /// </summary>
    public long OpenapiFileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string? OpenapiFileName { get; set; }

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; set; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 使用量计数.
    /// </summary>
    public int Counter { get; set; }
}
