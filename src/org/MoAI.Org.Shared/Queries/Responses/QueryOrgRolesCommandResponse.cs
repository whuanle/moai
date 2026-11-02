using MoAI.Infra.Models;

namespace MoAI.Org.Queries.Responses;

/// <summary>
/// 组织平铺项.
/// </summary>
public class QueryOrgRolesCommandResponseItem : AuditsInfo
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
    /// 上级角色 id，null=根角色.
    /// </summary>
    public int? ParentId { get; init; }

    /// <summary>
    /// 物化路径（/{id}/ 链）.
    /// </summary>
    public string Path { get; init; } = default!;

    /// <summary>
    /// 直属用户数.
    /// </summary>
    public int UserCount { get; init; }

    /// <summary>
    /// 是否有子角色.
    /// </summary>
    public bool HasChildren { get; init; }

    /// <summary>
    /// 同级展示顺序，从小到大.
    /// </summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// 组织列表结果.
/// </summary>
public class QueryOrgRolesCommandResponse
{
    /// <summary>
    /// 角色平铺列表，按 path 排序（天然层级序）.
    /// </summary>
    public List<QueryOrgRolesCommandResponseItem> Items { get; init; } = [];
}
