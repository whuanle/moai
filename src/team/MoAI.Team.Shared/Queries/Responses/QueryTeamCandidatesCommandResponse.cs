namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队候选用户列表响应.
/// </summary>
public class QueryTeamCandidatesCommandResponse
{
    /// <summary>
    /// 候选用户集合.
    /// </summary>
    public IReadOnlyList<TeamCandidateItem> Items { get; set; } = new List<TeamCandidateItem>();
}

/// <summary>
/// 团队候选用户项.
/// </summary>
public class TeamCandidateItem
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
}
