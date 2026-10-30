using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 组织角色：全局层级角色树节点（与团队无关），parentId 记录父子关系，path 为物化路径（/{id}/ 链，前缀匹配即得子树）.
/// </summary>
public partial class OrgRoleEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 角色名称，同一父角色下唯一.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 上级角色 id，null=根角色.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// 物化路径：根为 /{id}/，子级逐段追加（如 /1/3/8/）；带尾斜杠保证前缀匹配无歧义.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
