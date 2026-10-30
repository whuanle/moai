using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Timer.Queries.Responses;

/// <summary>
/// 团队定时器项.
/// </summary>
public class TeamTimerItem : AuditsInfo
{
    /// <summary>
    /// 定时器 id.
    /// </summary>
    public Guid TimerId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 定时器名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 绑定的应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 绑定的应用名称（应用已删除为空）.
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// 绑定的应用类型.
    /// </summary>
    public AppType AppType { get; set; }

    /// <summary>
    /// 绑定的应用发布状态，0=草稿 1=已发布.
    /// </summary>
    public short AppPublishStatus { get; set; }

    /// <summary>
    /// 是否启用调度.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 调度 cron 表达式（UTC）.
    /// </summary>
    public string CronExpression { get; set; } = default!;

    /// <summary>
    /// 每次执行时作为提问发送给应用的内容.
    /// </summary>
    public string InputContent { get; set; } = default!;

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
    /// 最近一次执行状态（等待/执行中/成功/失败/待重试），未执行过为 null；前端据此展示执行状态并在活跃时轮询刷新.
    /// </summary>
    public TimerExecutionStatus? LastExecutionStatus { get; set; }

    /// <summary>
    /// 下次调度时间（Hangfire），未启用或无调度为 null.
    /// </summary>
    public DateTimeOffset? NextRunTime { get; set; }
}
