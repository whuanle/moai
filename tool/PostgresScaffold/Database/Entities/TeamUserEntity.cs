using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 团队成员，用户与团队多对多关联.
/// </summary>
public partial class TeamUserEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联team.id（仓库约定不建物理外键）.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 成员用户ID，逻辑关联user.id（仓库约定不建物理外键）.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 成员角色：0=Owner(所有者，可解散/转让/管理一切) 1=Admin(可管理成员、创建团队资源) 2=Member(普通成员)，新成员默认2.
    /// </summary>
    public int Role { get; set; }

    /// <summary>
    /// 软删除：false=在团队中，true=已移出（审计钩子经接口适配自动写入）.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 邀请人用户ID，审计钩子插入时自动填充.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 加入时间，审计钩子自动填充，默认timezone(utc,now()).
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人用户ID，角色变更/移除时审计钩子自动填充.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间，审计钩子插入/更新/删除时自动刷新.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
