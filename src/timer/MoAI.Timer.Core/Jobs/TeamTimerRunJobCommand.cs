using MoAI.Hangfire.Models;

namespace MoAI.Timer.Jobs;

/// <summary>
/// 团队定时器调度任务：由 Hangfire 按 cron（UTC）触发，为定时器创建一条执行记录并无头执行绑定的应用.
/// 定时器被删除或停用时返回 <see cref="RecuringJobResponse.IsCancel"/> 让调度器移除该任务.
/// </summary>
public class TeamTimerRunJobCommand : RecuringJobCommand<TeamTimerJobParams>
{
}
