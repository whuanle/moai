using System;
using System.Collections.Generic;
using MoAI.AI.Models;

namespace MoAI.Memory.Models;

/// <summary>
/// 用户在某应用的记忆解析结果（null 表示应用未开启记忆）.
/// </summary>
public sealed class MemoryResolution
{
    /// <summary>
    /// 应用记忆配置.
    /// </summary>
    public required AgentMemorySettings Settings { get; init; }

    /// <summary>
    /// 用户生效记忆模式（AgentMemorySettings.UserMode* 常量）.
    /// </summary>
    public int UserMode { get; init; }

    /// <summary>
    /// 记忆是否对该用户生效（开启且非关闭模式）.
    /// </summary>
    public bool IsEnabled => UserMode != AgentMemorySettings.UserModeOff;
}

/// <summary>
/// 记忆检索查询.
/// </summary>
public sealed class MemorySearchQuery
{
    /// <summary>
    /// 当前应用 id（记忆注入/工具调用的发起应用）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 用户 id（记忆隔离维度）.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 查询文本.
    /// </summary>
    public required string QueryText { get; init; }

    /// <summary>
    /// 召回条数上限.
    /// </summary>
    public int TopK { get; init; } = AgentMemorySettings.DefaultRetrievalTopK;

    /// <summary>
    /// 相似度阈值（0~1）.
    /// </summary>
    public double MinScore { get; init; } = AgentMemorySettings.DefaultMinScore;

    /// <summary>
    /// 限定记忆类型；空表示按应用配置的全部启用类型.
    /// </summary>
    public IReadOnlyList<int>? Types { get; init; }
}

/// <summary>
/// 记忆保存请求.
/// </summary>
public sealed class MemorySaveRequest
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 团队 id（冗余，shard 登记用）.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 来源会话 id（可为空 Guid 表示无来源）.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 记忆内容.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 记忆类型（AgentMemorySettings.Type* 常量）.
    /// </summary>
    public int MemoryType { get; init; } = AgentMemorySettings.TypeSemantic;

    /// <summary>
    /// 应用记忆配置（调用方已解析；决定类型开关/去重/容量与向量模型）.
    /// </summary>
    public required AgentMemorySettings Settings { get; init; }
}

/// <summary>
/// 记忆条目.
/// </summary>
public sealed class MemoryItem
{
    /// <summary>
    /// 记忆 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 记忆类型（0 语义 / 1 情景 / 2 程序）.
    /// </summary>
    public int MemoryType { get; init; }

    /// <summary>
    /// 记忆内容.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 相似度得分（列表场景为 0）.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 最近更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }
}
