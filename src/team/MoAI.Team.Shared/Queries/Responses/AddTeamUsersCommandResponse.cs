namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 批量添加团队成员结果.
/// </summary>
public class AddTeamUsersCommandResponse
{
    /// <summary>
    /// 成功加入团队的成员数.
    /// </summary>
    public int AddedCount { get; init; }

    /// <summary>
    /// 已是团队成员被跳过的用户 id 集合.
    /// </summary>
    public IReadOnlyList<long> SkippedUserIds { get; init; } = [];

    /// <summary>
    /// 不存在被跳过的用户 id 集合.
    /// </summary>
    public IReadOnlyList<long> NotFoundUserIds { get; init; } = [];
}
