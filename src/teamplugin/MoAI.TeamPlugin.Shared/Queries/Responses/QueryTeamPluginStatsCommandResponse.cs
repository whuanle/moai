using System;
using System.Collections.Generic;

namespace MoAI.TeamPlugin.Queries.Responses;

/// <summary>
/// 团队插件使用统计响应（仅本团队自有插件）：概览计数 + 调用量 TOP + 分类分布 + 近 30 天趋势.
/// </summary>
public class QueryTeamPluginStatsCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 团队自有插件总数.
    /// </summary>
    public int TotalPlugins { get; init; }

    /// <summary>
    /// MCP 插件数量.
    /// </summary>
    public int McpCount { get; init; }

    /// <summary>
    /// OpenAPI 插件数量.
    /// </summary>
    public int OpenApiCount { get; init; }

    /// <summary>
    /// 累计调用次数（本团队插件 counter 合计）.
    /// </summary>
    public long TotalCalls { get; init; }

    /// <summary>
    /// 函数总数（自定义=函数表计数，动态=注册可用计 1）.
    /// </summary>
    public long TotalFunctions { get; init; }

    /// <summary>
    /// 调用量 TOP（按累计使用量降序，最多 5 条）.
    /// </summary>
    public List<QueryTeamPluginStatsTopItem> Top { get; init; } = new();

    /// <summary>
    /// 分类分布（含未分类，key=0 时 classifyName 为空由前端兜底文案）.
    /// </summary>
    public List<QueryTeamPluginStatsClassifyItem> Classifies { get; init; } = new();

    /// <summary>
    /// 近 30 天调用趋势（区间内无数据日期补 0）.
    /// </summary>
    public QueryTeamPluginStatsTrendResponse Trend { get; init; } = new();
}

/// <summary>
/// 团队插件调用量 TOP 项.
/// </summary>
public class QueryTeamPluginStatsTopItem
{
    /// <summary>
    /// 插件记录 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 展示名称（title 优先，空则取 pluginName）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 累计使用量.
    /// </summary>
    public long Counter { get; init; }

    /// <summary>
    /// 头像（存储 ObjectKey）.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;
}

/// <summary>
/// 团队插件分类分布项.
/// </summary>
public class QueryTeamPluginStatsClassifyItem
{
    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 分类名称.
    /// </summary>
    public string? ClassifyName { get; init; }

    /// <summary>
    /// 插件数量.
    /// </summary>
    public int Count { get; init; }
}

/// <summary>
/// 团队插件调用趋势（近 30 天，无数据日期补 0）.
/// </summary>
public class QueryTeamPluginStatsTrendResponse
{
    /// <summary>
    /// 按日数据点.
    /// </summary>
    public List<QueryTeamPluginStatsTrendItem> Items { get; init; } = new();

    /// <summary>
    /// 区间合计.
    /// </summary>
    public long Total { get; init; }

    /// <summary>
    /// 起始日期.
    /// </summary>
    public DateOnly StartDate { get; init; }

    /// <summary>
    /// 结束日期.
    /// </summary>
    public DateOnly EndDate { get; init; }
}

/// <summary>
/// 趋势单日数据点.
/// </summary>
public class QueryTeamPluginStatsTrendItem
{
    /// <summary>
    /// 统计日期.
    /// </summary>
    public DateOnly Date { get; init; }

    /// <summary>
    /// 当日合计.
    /// </summary>
    public long Count { get; init; }
}
