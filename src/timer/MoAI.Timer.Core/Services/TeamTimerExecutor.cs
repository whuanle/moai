using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;

#pragma warning disable CA1031 // 执行器顶层捕获全部异常并落入执行记录

namespace MoAI.Timer.Services;

/// <summary>
/// 定时器执行器：一条执行记录从 Pending → Running →（成功 | 待重试 → …→）成功/失败 的状态机。
/// 失败按定时器配置延迟重试（Hangfire 延迟任务，不占工作线程），重试在同一执行记录内累计尝试次数.
/// </summary>
[InjectOnScoped]
public class TeamTimerExecutor
{
    private const int MaxErrorMessageLength = 4000;

    private readonly DatabaseContext _databaseContext;
    private readonly TeamTimerAppInvoker _appInvoker;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<TeamTimerExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerExecutor"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="appInvoker">应用无头调用器.</param>
    /// <param name="backgroundJobClient">Hangfire 后台任务客户端（延迟重试/入队）.</param>
    /// <param name="logger">日志.</param>
    public TeamTimerExecutor(
        DatabaseContext databaseContext,
        TeamTimerAppInvoker appInvoker,
        IBackgroundJobClient backgroundJobClient,
        ILogger<TeamTimerExecutor> logger)
    {
        _databaseContext = databaseContext;
        _appInvoker = appInvoker;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    /// <summary>
    /// 执行一次（Hangfire 任务入口：调度触发、手动触发与延迟重试都落到这里）.
    /// </summary>
    /// <param name="executionId">执行记录 id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RunAsync(Guid executionId)
    {
        var execution = await _databaseContext.TeamTimerExecutions
            .FirstOrDefaultAsync(x => x.Id == executionId);

        // 已终态（成功/失败）说明本次为重复投递，直接跳过
        if (execution == null || execution.Status == (int)TimerExecutionStatus.Success || execution.Status == (int)TimerExecutionStatus.Failed)
        {
            return;
        }

        var timer = await _databaseContext.TeamTimers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == execution.TimerId);
        if (timer == null)
        {
            await FailFinalAsync(execution, "定时器已删除，不再执行.");
            return;
        }

        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == timer.AppId);
        if (app == null || app.IsDisable)
        {
            // 应用被删除/禁用非瞬时错误，重试无意义，直接终态
            await FailFinalAsync(execution, "绑定的应用已删除或被禁用，无法执行.");
            return;
        }

        execution.Status = (int)TimerExecutionStatus.Running;
        execution.StartTime ??= DateTimeOffset.Now;
        execution.AttemptCount += 1;
        execution.NextRetryTime = null;
        execution.ErrorMessage = null;
        execution.Result = null;
        await _databaseContext.SaveChangesAsync();

        try
        {
            var (result, appInstanceId) = await _appInvoker.InvokeAsync(timer, app, CancellationToken.None);

            execution.Status = (int)TimerExecutionStatus.Success;
            execution.Result = result;
            execution.AppInstanceId = appInstanceId;
            execution.EndTime = DateTimeOffset.Now;
            execution.DurationMs = (long?)(execution.EndTime - execution.StartTime)?.TotalMilliseconds;
            await _databaseContext.SaveChangesAsync();

            _logger.LogInformation(
                "定时器执行成功. TimerId={TimerId}, ExecutionId={ExecutionId}, Attempt={Attempt}, DurationMs={DurationMs}",
                timer.Id,
                executionId,
                execution.AttemptCount,
                execution.DurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "定时器执行失败. TimerId={TimerId}, ExecutionId={ExecutionId}, Attempt={Attempt}", timer.Id, executionId, execution.AttemptCount);

            execution.ErrorMessage = Truncate(ex.ToString());

            // 已失败尝试次数（首次记 1）未超过重试上限则安排下一次延迟重试
            if (execution.AttemptCount <= timer.MaxRetryCount)
            {
                var delay = TimeSpan.FromSeconds(Math.Max(10, timer.RetryIntervalSeconds));
                execution.Status = (int)TimerExecutionStatus.Retrying;
                execution.NextRetryTime = DateTimeOffset.Now.Add(delay);
                await _databaseContext.SaveChangesAsync();

                _backgroundJobClient.Schedule<TeamTimerExecutor>(x => x.RunAsync(executionId), delay);
                return;
            }

            execution.Status = (int)TimerExecutionStatus.Failed;
            execution.EndTime = DateTimeOffset.Now;
            execution.DurationMs = (long?)(execution.EndTime - execution.StartTime)?.TotalMilliseconds;
            await _databaseContext.SaveChangesAsync();
        }
    }

    private async Task FailFinalAsync(TeamTimerExecutionEntity execution, string message)
    {
        execution.Status = (int)TimerExecutionStatus.Failed;
        execution.ErrorMessage = message;
        execution.EndTime = DateTimeOffset.Now;
        execution.DurationMs = (long?)(execution.EndTime - execution.StartTime)?.TotalMilliseconds;
        await _databaseContext.SaveChangesAsync();
    }

    private static string Truncate(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= MaxErrorMessageLength)
        {
            return text;
        }

        return text[..MaxErrorMessageLength];
    }
}
