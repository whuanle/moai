using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 组织角色成员绑定，用户与组织角色多对多关联.
/// </summary>
public partial class OrgUserRoleEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户 id，逻辑关联 user.id（仓库约定不建物理外键）.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 组织角色 id，逻辑关联 org_role.id.
    /// </summary>
    public int RoleId { get; set; }

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
