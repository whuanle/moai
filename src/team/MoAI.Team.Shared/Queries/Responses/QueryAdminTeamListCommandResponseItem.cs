using MoAI.Infra.Models;

namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 全部团队列表项（管理员）.
/// </summary>
public class QueryAdminTeamListCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 团队简介，空串=未填写.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 团队头像地址（公开访问 URL，空串=未设置）.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <summary>
    /// 成员数量.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// 团队负责人（Owner）的用户 id，团队无有效负责人时为 0.
    /// </summary>
    public long OwnerUserId { get; init; }

    /// <summary>
    /// 团队负责人的用户名，无有效负责人时为空串.
    /// </summary>
    public string OwnerUserName { get; init; } = string.Empty;

    /// <summary>
    /// 团队负责人的昵称，无有效负责人时为空串.
    /// </summary>
    public string OwnerNickName { get; init; } = string.Empty;

    /// <summary>
    /// 团队负责人的头像地址（公开访问 URL，空串=未设置）.
    /// </summary>
    public string OwnerAvatar { get; init; } = string.Empty;
}
