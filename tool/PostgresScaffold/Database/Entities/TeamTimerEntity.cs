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
/// 团队定时器：绑定应用按 cron 定时无头执行.
/// </summary>
public partial class TeamTimerEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队 id，逻辑关联 team.id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 定时器名称，团队内唯一.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 绑定的应用 id，逻辑关联 app.id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 绑定时的应用类型快照，0=Agent 1=流程.
    /// </summary>
    public int AppType { get; set; }

    /// <summary>
    /// 调度 cron 表达式（UTC，5 段标准格式）.
    /// </summary>
    public string CronExpression { get; set; } = default!;

    /// <summary>
    /// 每次执行时作为提问发送给应用的内容.
    /// </summary>
    public string InputContent { get; set; } = default!;

    /// <summary>
    /// 是否启用调度.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 失败重试次数（不含首次执行）.
    /// </summary>
    public int MaxRetryCount { get; set; }

    /// <summary>
    /// 重试间隔秒数.
    /// </summary>
    public int RetryIntervalSeconds { get; set; }

    /// <summary>
    /// 最近一次触发时间，未执行过为 null.
    /// </summary>
    public DateTimeOffset? LastRunTime { get; set; }

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
