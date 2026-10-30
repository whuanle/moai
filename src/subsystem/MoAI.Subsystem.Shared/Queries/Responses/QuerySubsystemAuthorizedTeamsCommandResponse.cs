namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 子系统已授权团队查询结果.
/// </summary>
public class QuerySubsystemAuthorizedTeamsCommandResponse
{
    /// <summary>
    /// 已授权团队 id 列表.
    /// </summary>
    public IReadOnlyList<int> TeamIds { get; init; } = [];
}
