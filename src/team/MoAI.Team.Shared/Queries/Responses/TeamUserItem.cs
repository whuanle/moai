namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队成员项.
/// </summary>
public class TeamUserItem
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; set; } = default!;

    /// <summary>
    /// 头像地址（公开访问 URL，空串=未设置）.
    /// </summary>
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int Role { get; set; }

    /// <summary>
    /// 加入时间.
    /// </summary>
    public DateTimeOffset JoinTime { get; set; }
}
