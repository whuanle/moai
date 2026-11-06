using MoAI.Infra.Models;

namespace MoAI.TeamAgent.Queries.Responses;

/// <summary>
/// 团队智能体列表项.
/// </summary>
public class TeamAgentListItem : AuditsInfo
{
    /// <summary>
    /// 智能体 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 智能体标识 key.
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
    /// 子智能体数量.
    /// </summary>
    public int SubCount { get; init; }
}
