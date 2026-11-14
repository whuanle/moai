using System.Collections.Generic;

namespace MoAI.TeamAgent.Queries.Responses;

/// <summary>
/// 团队智能体列表响应.
/// </summary>
public class QueryTeamAgentListCommandResponse
{
    /// <summary>
    /// 当前用户在团队中的角色，2=Owner 1=Admin 0=Member.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 智能体列表.
    /// </summary>
    public IReadOnlyList<TeamAgentListItem> Items { get; init; } = System.Array.Empty<TeamAgentListItem>();
}
