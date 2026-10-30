using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Timer.Queries.Responses;

/// <summary>
/// 团队定时器执行记录项.
/// </summary>
public class TeamTimerExecutionItem : AuditsInfo
{
    /// <summary>
    /// 执行记录 id.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// 定时器 id.
    /// </summary>
    public Guid TimerId { get; set; }

    /// <summary>
    /// 触发方式.
    /// </summary>
    public TimerTriggerType TriggerType { get; set; }

    /// <summary>
    /// 触发用户 id（手动=操作人，调度=定时器创建人）.
    /// </summary>
    public long TriggerUserId { get; set; }

    /// <summary>
    /// 触发用户名字.
    /// </summary>
    public string TriggerUserName { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态.
    /// </summary>
    public TimerExecutionStatus Status { get; set; }

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
    /// 定时执行产生的 Agent 会话 id（可在应用日志页查看完整对话），流程应用为 null.
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
}
