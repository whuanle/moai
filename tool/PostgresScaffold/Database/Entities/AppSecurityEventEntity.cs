using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

public partial class AppSecurityEventEntity : IFullAudited
{
    public Guid Id { get; set; }

    public int TeamId { get; set; }

    public Guid AppId { get; set; }

    public Guid? SessionId { get; set; }

    public long? UserId { get; set; }

    public string Scope { get; set; } = default!;

    public string RuleName { get; set; } = default!;

    public string RuleType { get; set; } = default!;

    public string Strategy { get; set; } = default!;

    public string Action { get; set; } = default!;

    public int HitCount { get; set; }

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
    /// 软删除，0=未删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
