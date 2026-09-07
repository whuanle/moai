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
/// 授权私有系统插件给哪些团队使用.
/// </summary>
public partial class PluginTeamAuthorizationEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 系统插件记录 id（plugin.id，逻辑关联，仓库约定不建物理外键）.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// 授权团队id，该团队成员可以使用该私有插件（逻辑关联 team.id）.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 软删除：0=未删除，非0=已删除（审计钩子自动写入）.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// 创建人用户ID，审计钩子插入时自动填充.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间，审计钩子自动填充，默认timezone(utc,now()).
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人用户ID，审计钩子更新/删除时自动填充.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间，审计钩子插入/更新/删除时自动刷新.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
