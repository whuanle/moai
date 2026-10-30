namespace MoAI.Org.Queries.Responses;

/// <summary>
/// 用户持有的组织角色项.
/// </summary>
public class QueryUserOrgRolesCommandResponseItem
{
    /// <summary>
    /// 角色 id.
    /// </summary>
    public int OrgRoleId { get; init; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 物化路径（/{id}/ 链）.
    /// </summary>
    public string Path { get; init; } = default!;
}

/// <summary>
/// 用户组织角色查询结果.
/// </summary>
public class QueryUserOrgRolesCommandResponse
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = default!;

    /// <summary>
    /// 持有的角色列表，按 path 排序.
    /// </summary>
    public List<QueryUserOrgRolesCommandResponseItem> Items { get; init; } = [];
}
