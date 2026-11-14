using System;
using System.Collections.Generic;
using MoAI.Infra.Models;
using MoAI.TeamAgent.Models;

namespace MoAI.TeamAgent.Queries.Responses;

/// <summary>
/// 团队智能体详情.
/// </summary>
public class TeamAgentDetailItem : AuditsInfo
{
    /// <summary>
    /// 智能体 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 智能体标识 key（创建后不可变更）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 智能体名称（展示名）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 智能体描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 头像文件的 ObjectKey（空串=未设置）.
    /// </summary>
    public string AvatarObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 是否系统内置智能体（团队创建时自动种入，不可删除、可编辑定制）.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 主智能体系统提示词.
    /// </summary>
    public string MainPrompt { get; init; } = string.Empty;

    /// <summary>
    /// 主智能体插件依赖.
    /// </summary>
    public IReadOnlyList<Guid> MainPluginIds { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 主智能体知识库依赖.
    /// </summary>
    public IReadOnlyList<long> MainWikiIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 主智能体知识图谱依赖.
    /// </summary>
    public IReadOnlyList<long> MainGraphIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 子智能体列表.
    /// </summary>
    public IReadOnlyList<TeamAgentSubItem> Subs { get; init; } = Array.Empty<TeamAgentSubItem>();
}

/// <summary>
/// 团队智能体详情响应.
/// </summary>
public class QueryTeamAgentDetailCommandResponse
{
    /// <summary>
    /// 智能体详情.
    /// </summary>
    public TeamAgentDetailItem Agent { get; init; } = default!;
}
