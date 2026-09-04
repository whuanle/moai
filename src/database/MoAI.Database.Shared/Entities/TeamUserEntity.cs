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
    public int Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联team.id（仓库约定不建物理外键）.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 成员用户ID，逻辑关联user.id（仓库约定不建物理外键）.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 成员角色.
    /// </summary>
    public int Role { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }

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
