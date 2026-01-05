using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 外部系统的用户.
/// </summary>
public partial class ExternalUserEntity : IFullAudited
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 所属的外部应用id.
    /// </summary>
    public int ExternalAppId { get; set; }

    /// <summary>
    /// 外部用户标识.
    /// </summary>
    public string UserUid { get; set; } = default!;

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }

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
}
