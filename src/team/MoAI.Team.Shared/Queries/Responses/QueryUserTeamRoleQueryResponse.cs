using MoAI.Team.Models;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询用户在团队角色的响应.
/// </summary>
public class QueryUserTeamRoleQueryResponse
{
    /// <summary>
    /// 用户在团队的角色.
    /// </summary>
    public TeamRole Role { get; init; }
}
