using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件使用统计响应（全团队汇总）：概览计数 + 调用量 TOP + 分类分布 + 近 30 天趋势.
/// </summary>
public class QueryPluginStatsCommandResponse
{
    /// <summary>
    /// 插件总数（系统侧 + 全部团队自有插件，含内存发现的静态插件）.
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
    /// 累计调用次数（插件表 counter 合计，含全部团队）.
    /// </summary>
    public long TotalCalls { get; init; }

    /// <summary>
    /// 函数总数（自定义=函数表计数，静态/动态=注册可用计 1）.
    /// </summary>
    public long TotalFunctions { get; init; }

    /// <summary>
    /// 调用量 TOP（按累计使用量降序，最多 5 条；团队自有插件带团队名称）.
    /// </summary>
    public List<QueryPluginStatsTopItem> Top { get; init; } = new();

    /// <summary>
    /// 分类分布（含未分类，key=0 时 classifyName 为空由前端兜底文案）.
    /// </summary>
    public List<QueryPluginStatsClassifyItem> Classifies { get; init; } = new();

    /// <summary>
    /// 近 30 天调用趋势（区间内无数据日期补 0）.
    /// </summary>
    public QueryPluginStatsTrendResponse Trend { get; init; } = new();
}

/// <summary>
/// 插件调用量 TOP 项.
/// </summary>
public class QueryPluginStatsTopItem
{
    /// <summary>
    /// 插件记录 id（内存发现的静态插件为 Guid.Empty）.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 展示名称（title 优先，空则取 pluginName）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 归属团队 id，0 表示系统侧.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 归属团队名称，系统侧为 null.
    /// </summary>
    public string? TeamName { get; init; }

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
/// 插件分类分布项.
/// </summary>
public class QueryPluginStatsClassifyItem
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
/// 插件调用趋势（近 30 天，无数据日期补 0）.
/// </summary>
public class QueryPluginStatsTrendResponse
{
    /// <summary>
    /// 按日数据点.
    /// </summary>
    public List<QueryPluginStatsTrendItem> Items { get; init; } = new();

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
public class QueryPluginStatsTrendItem
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
