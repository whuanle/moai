using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 登录 IP 封禁：风控自动封禁（爆破/撞库）或管理员手动封禁，到期自动解封，管理员可提前解封.
/// </summary>
public partial class LoginIpBanEntity : ICreationAudited
{
    /// <summary>
    /// 主键.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 封禁的 IP.
    /// </summary>
    public string Ip { get; set; } = default!;

    /// <summary>
    /// 封禁原因.
    /// </summary>
    public string Reason { get; set; } = default!;

    /// <summary>
    /// 来源（auto:R1=IP 爆破自动 / auto:R2=撞库喷洒自动 / manual=管理员手动）.
    /// </summary>
    public string SourceRule { get; set; } = default!;

    /// <summary>
    /// 封禁到期时间.
    /// </summary>
    public DateTimeOffset ExpireTime { get; set; }

    /// <summary>
    /// 是否已解封.
    /// </summary>
    public bool IsLifted { get; set; }

    /// <summary>
    /// 解封人用户 id，未解封为 0.
    /// </summary>
    public long LiftUserId { get; set; }

    /// <summary>
    /// 解封时间，未解封为空.
    /// </summary>
    public DateTimeOffset? LiftTime { get; set; }

    /// <inheritdoc/>
    public long CreateUserId { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset CreateTime { get; set; }
}
