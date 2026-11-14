using MoAI.Database.Enums;

namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 子系统组织授权配置回显.
/// </summary>
public class QuerySubsystemOrgGrantsCommandResponse
{
    /// <summary>
    /// 授权记录列表.
    /// </summary>
    public List<SubsystemOrgGrantInfoItem> Items { get; init; } = [];
}

/// <summary>
/// 子系统组织授权记录项（回显用，含角色信息）.
/// </summary>
public class SubsystemOrgGrantInfoItem
{
    /// <summary>
    /// 组织角色 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string RoleName { get; init; } = default!;

    /// <summary>
    /// 角色物化路径（/{id}/ 链）.
    /// </summary>
    public string RolePath { get; init; } = default!;

    /// <summary>
    /// 授权方向：self/up/down.
    /// </summary>
    public OrgGrantScope Scope { get; init; }
}
