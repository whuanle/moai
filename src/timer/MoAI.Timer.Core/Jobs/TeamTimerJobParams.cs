using System;

namespace MoAI.Timer.Jobs;

/// <summary>
/// 团队定时器调度任务的触发参数.
/// </summary>
public class TeamTimerJobParams
{
    /// <summary>
    /// 定时器 id.
    /// </summary>
    public Guid TimerId { get; init; }
}
