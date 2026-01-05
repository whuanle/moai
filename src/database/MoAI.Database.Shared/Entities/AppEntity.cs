using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 应用.
/// </summary>
public partial class AppEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 公开到团队外使用.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool IsForeign { get; set; }

    /// <summary>
    /// 应用类型，普通应用=0,流程编排=1.
    /// </summary>
    public int AppType { get; set; }

    /// <summary>
    /// 头像 objectKey.
    /// </summary>
    public string Avatar { get; set; } = default!;

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
