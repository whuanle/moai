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
/// 应用记忆分片登记：用户在某应用下的长期记忆行数，支撑跨应用发现与清空.
/// </summary>
public partial class AppMemoryShardEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 应用 id，逻辑关联 app.id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 所属团队 id，逻辑关联 app.team_id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 用户 id，逻辑关联 user.id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 该用户在该应用向量集合中的记忆条数.
    /// </summary>
    public int MemoryCount { get; set; }

    /// <summary>
    /// 最近一次记忆写入时间.
    /// </summary>
    public DateTimeOffset LastActiveTime { get; set; }

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
