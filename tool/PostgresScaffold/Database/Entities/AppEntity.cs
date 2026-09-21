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
    /// 分类id.
    /// </summary>
    public int ClassifyId { get; set; }

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

    /// <summary>
    /// 发布状态，0=草稿 1=已发布.
    /// </summary>
    public short PublishStatus { get; set; }

    /// <summary>
    /// 发布时间，未发布为 null.
    /// </summary>
    public DateTimeOffset? PublishTime { get; set; }

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// 是否需要授权访问，外部应用设置才有效.
    /// </summary>
    public bool IsAuth { get; set; }
}
