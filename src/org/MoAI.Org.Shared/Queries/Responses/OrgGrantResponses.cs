using MoAI.Org.Services;

namespace MoAI.Org.Queries.Responses;

/// <summary>
/// 整体覆盖组织授权结果.
/// </summary>
public class SetOrgGrantsCommandResponse
{
    /// <summary>
    /// 生效授权记录数.
    /// </summary>
    public int Count { get; init; }
}

/// <summary>
/// 资源组织授权配置回显.
/// </summary>
public class QueryOrgGrantsCommandResponse
{
    /// <summary>
    /// 授权记录列表.
    /// </summary>
    public List<OrgGrantInfo> Items { get; init; } = [];
}

/// <summary>
/// 资源有效授权角色（已按方向规则展开）.
/// </summary>
public class QueryOrgGrantRolesCommandResponse
{
    /// <summary>
    /// 有效角色列表.
    /// </summary>
    public List<OrgGrantedRoleInfo> Items { get; init; } = [];
}

/// <summary>
/// 资源有效授权用户.
/// </summary>
public class QueryOrgGrantUsersCommandResponse
{
    /// <summary>
    /// 有效用户 id 列表.
    /// </summary>
    public List<long> UserIds { get; init; } = [];
}

/// <summary>
/// 组织授权判定结果.
/// </summary>
public class CheckOrgGrantCommandResponse
{
    /// <summary>
    /// 是否放通.
    /// </summary>
    public bool Allowed { get; init; }
}
