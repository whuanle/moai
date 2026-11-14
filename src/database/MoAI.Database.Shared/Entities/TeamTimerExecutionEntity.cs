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
/// 团队定时器执行记录：一次触发一行，失败重试在同一行内累计尝试次数.
/// </summary>
public partial class TeamTimerExecutionEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 定时器 id，逻辑关联 team_timer.id.
    /// </summary>
    public Guid TimerId { get; set; }

    /// <summary>
    /// 所属团队 id，逻辑关联 team_timer.team_id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 触发方式，0=调度 1=手动.
    /// </summary>
    public int TriggerType { get; set; }

    /// <summary>
    /// 触发用户 id（手动=操作人，调度=定时器创建人）.
    /// </summary>
    public long TriggerUserId { get; set; }

    /// <summary>
    /// 执行状态，0=等待 1=执行中 2=成功 3=失败 4=待重试.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 已尝试次数（首次执行记 1）.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// 首次开始执行时间，未开始为 null.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间，未结束为 null.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 总耗时毫秒，未结束为 null.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 下次重试时间，仅待重试状态有值.
    /// </summary>
    public DateTimeOffset? NextRetryTime { get; set; }

    /// <summary>
    /// 流程应用实例 id，Agent 应用为 null.
    /// </summary>
    public string? AppInstanceId { get; set; }

    /// <summary>
    /// 定时执行产生的 Agent 会话 id（app_agent_session，UserType=timer），流程应用为 null.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 应用回答文本.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 失败原因.
    /// </summary>
    public string? ErrorMessage { get; set; }

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
