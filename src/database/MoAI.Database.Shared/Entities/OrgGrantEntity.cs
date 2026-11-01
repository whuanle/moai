using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 统一组织授权：把资源定向放通给某条组织线，跨资源类型共用，一条记录只挂一个组织.
/// </summary>
public partial class OrgGrantEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 资源类型（OrgGrantResourceType 枚举值）.
    /// </summary>
    public int ResourceType { get; set; }

    /// <summary>
    /// 资源主键的字符串形态（应用为 Guid、知识库等为 long，统一存字符串）.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// 组织 id，逻辑关联 org_role.id.
    /// </summary>
    public int OrgRoleId { get; set; }

    /// <summary>
    /// 授权方向（OrgGrantScope 枚举值：self 仅该角色 / up 含祖先 / down 含子树）.
    /// </summary>
    public int Scope { get; set; }

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
