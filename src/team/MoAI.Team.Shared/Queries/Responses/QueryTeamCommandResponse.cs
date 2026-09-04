namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队详情响应.
/// </summary>
public class QueryTeamCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 团队简介.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队头像地址（公开访问 URL，空串=未设置）.
    /// </summary>
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 我在团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 团队负责人（Owner）的用户 id.
    /// </summary>
    public long OwnerUserId { get; set; }

    /// <summary>
    /// 团队负责人的用户名.
    /// </summary>
    public string OwnerUserName { get; set; } = default!;

    /// <summary>
    /// 团队负责人的昵称.
    /// </summary>
    public string OwnerNickName { get; set; } = default!;

    /// <summary>
    /// 团队负责人的头像地址（公开访问 URL，空串=未设置）.
    /// </summary>
    public string OwnerAvatar { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
