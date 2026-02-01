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
/// oauth2.0对接.
/// </summary>
public partial class UserOauthEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 供应商id,对应oauth_connection表.
    /// </summary>
    public Guid ProviderId { get; set; }

    /// <summary>
    /// 用户oauth对应的唯一id.
    /// </summary>
    public string Sub { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
