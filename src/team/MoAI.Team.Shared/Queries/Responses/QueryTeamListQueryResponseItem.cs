using MoAI.Storage.Queries;
using MoAI.Team.Models;

namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队列表项.
/// </summary>
public class QueryTeamListQueryResponseItem : IAvatarPath
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <inheritdoc/>
    public string Avatar { get; set; } = default!;

    /// <inheritdoc/>
    public string AvatarKey { get; set; } = default!;

    /// <summary>
    /// 创建人ID.
    /// </summary>
    public int CreateUserId { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 当前用户在团队的角色.
    /// </summary>
    public TeamRole Role { get; init; }

    /// <summary>
    /// 成员数量.
    /// </summary>
    public int MemberCount { get; init; }
}
