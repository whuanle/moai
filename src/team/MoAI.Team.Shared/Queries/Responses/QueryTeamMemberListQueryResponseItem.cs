using MoAI.Storage.Queries;
using MoAI.Team.Models;

namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队成员列表项.
/// </summary>
public class QueryTeamMemberListQueryResponseItem : IAvatarPath
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NiceName { get; init; } = default!;

    /// <inheritdoc/>
    public string Avatar { get; set; } = default!;

    /// <inheritdoc/>
    public string AvatarKey { get; set; } = default!;

    /// <summary>
    /// 在团队的角色.
    /// </summary>
    public TeamRole Role { get; init; }

    /// <summary>
    /// 加入时间.
    /// </summary>
    public DateTimeOffset JoinTime { get; init; }
}
