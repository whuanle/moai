using MoAI.Database.Enums;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 系统级接入自身授权快照.
/// </summary>
public class QuerySubsysGrantsCommandResponse
{
    /// <summary>团队授权范围.</summary>
    public SubsystemTeamScope TeamScope { get; init; }

    /// <summary>指定授权的团队 id 集合（全量团队范围时为空）.</summary>
    public IReadOnlyList<long> TeamIds { get; init; } = [];

    /// <summary>组织授权记录（含方向）.</summary>
    public IReadOnlyList<QuerySubsysOrgGrantItem> OrgGrants { get; init; } = [];
}

/// <summary>
/// 组织授权项.
/// </summary>
public class QuerySubsysOrgGrantItem
{
    /// <summary>组织角色 id.</summary>
    public int OrgRoleId { get; init; }

    /// <summary>授权方向.</summary>
    public OrgGrantScope Scope { get; init; }
}
