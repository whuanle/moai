using System;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库外部源列表项.
/// </summary>
public class WikiSourceItem : AuditsInfo
{
    /// <summary>
    /// 外部源 id.
    /// </summary>
    public Guid SourceId { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 外部源类型.
    /// </summary>
    public WikiSourceType SourceType { get; init; }

    /// <summary>
    /// 外部源名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool IsEnable { get; init; }

    /// <summary>
    /// 定时同步 cron 表达式（UTC），空表示未开启.
    /// </summary>
    public string Cron { get; init; } = string.Empty;

    /// <summary>
    /// 是否开启飞书事件订阅.
    /// </summary>
    public bool IsEventSubscription { get; init; }

    /// <summary>
    /// 外部源工作流配置，为 null 表示回退知识库默认工作流.
    /// </summary>
    public WikiWorkflowConfig? WorkflowConfig { get; init; }

    /// <summary>
    /// 飞书文档源配置（不含任何密钥）.
    /// </summary>
    public WikiSourceFeishuConfig? Feishu { get; init; }

    /// <summary>
    /// 网页爬虫源配置.
    /// </summary>
    public WikiSourceCrawlerConfig? Crawler { get; init; }

    /// <summary>
    /// 绑定的飞书应用连接名称.
    /// </summary>
    public string? FeishuAppName { get; init; }

    /// <summary>
    /// 绑定的飞书开放平台 AppID.
    /// </summary>
    public string? FeishuAppOpenId { get; init; }

    /// <summary>
    /// 绑定的飞书应用长连接是否在线.
    /// </summary>
    public bool FeishuAppOnline { get; init; }

    /// <summary>
    /// 最近一次同步状态.
    /// </summary>
    public WikiSourceSyncStatus LastSyncStatus { get; init; }

    /// <summary>
    /// 最近一次同步时间.
    /// </summary>
    public DateTimeOffset? LastSyncTime { get; init; }

    /// <summary>
    /// 最近一次同步结果摘要或错误信息.
    /// </summary>
    public string LastSyncMessage { get; init; } = string.Empty;

    /// <summary>
    /// 已同步的文档数量.
    /// </summary>
    public int DocumentCount { get; init; }
}
