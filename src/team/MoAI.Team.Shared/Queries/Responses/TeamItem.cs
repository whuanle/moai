namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队项.
/// </summary>
public class TeamItem
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
    /// 成员数量.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
