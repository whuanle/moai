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
/// 系统接入.
/// </summary>
public partial class ExternalAppEntity : IFullAudited
{
    /// <summary>
    /// app_id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 头像objectKey.
    /// </summary>
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 应用密钥.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 禁用.
    /// </summary>
    public bool IsDsiable { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
