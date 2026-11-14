using System;
using System.Collections.Generic;

namespace MoAI.Memory.Queries.Responses;

/// <summary>
/// 本人记忆总览响应（记忆管理页入口数据）.
/// </summary>
public class QueryMyMemoryOverviewResponse
{
    /// <summary>
    /// 本人已有记忆的应用列表（按最近活跃倒序）.
    /// </summary>
    public IReadOnlyList<MyMemoryAppItem> Apps { get; init; } = [];

    /// <summary>
    /// 全局记忆概况.
    /// </summary>
    public MyMemoryGlobalSummary Global { get; init; } = new();
}

/// <summary>
/// 本人已有记忆的应用条目.
/// </summary>
public class MyMemoryAppItem
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string AppName { get; init; } = string.Empty;

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 所属团队名称.
    /// </summary>
    public string TeamName { get; init; } = string.Empty;

    /// <summary>
    /// 本人在该应用的记忆条数.
    /// </summary>
    public int MemoryCount { get; init; }

    /// <summary>
    /// 最近活跃时间.
    /// </summary>
    public DateTimeOffset LastActiveTime { get; init; }
}

/// <summary>
/// 全局记忆概况.
/// </summary>
public class MyMemoryGlobalSummary
{
    /// <summary>
    /// 平台是否开启全局记忆.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 本人的全局记忆条数.
    /// </summary>
    public int MyCount { get; init; }
}
