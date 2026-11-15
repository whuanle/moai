#pragma warning disable CA1031 // 清理任务不能因为单个定义失败而中断

using System.Diagnostics.CodeAnalysis;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 自动清理无法解析任务体的遗留 recurring job 定义.
/// </summary>
/// <remarks>
/// 删除/改名任务体类型时，Redis 里的 recurring job 定义不会跟着消失，Hangfire 调度器
/// 每轮入队都会 JobLoadException 并每 10 秒重试一次（日志持续刷错误）。本服务启动后
/// 扫描全部 recurring job，把「定义还在、但本进程已无法解析出可执行任务」的条目幂等删除。
/// <para>
/// 安全边界：只有 <see cref="RecurringJobDto.CreatedAt"/> 早于 <see cref="GracePeriod"/> 的定义才会被删除，
/// 避免滚动发布期间旧实例误删新版本刚注册的任务；扫描本身只读任务定义，不触碰
/// <c>moai:counter:*</c> 等业务计数与历史执行记录。
/// </para>
/// </remarks>
[SuppressMessage("Performance", "CA1812:避免未实例化的内部类", Justification = "由 DI 容器通过 AddHostedService 实例化")]
internal sealed class ObsoleteRecurringJobCleaner : BackgroundService
{
    /// <summary>
    /// 宽限期：新版本注册的任务在旧版本进程里解析失败属正常，超过该时长仍解析失败才视为遗留.
    /// </summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    /// <summary>
    /// 扫描间隔：启动后立即扫一次，之后定期兜底（覆盖回滚、长时间不重启等场景）.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly ILogger<ObsoleteRecurringJobCleaner> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObsoleteRecurringJobCleaner"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="hostApplicationLifetime"></param>
    /// <param name="serviceScopeFactory"></param>
    public ObsoleteRecurringJobCleaner(
        ILogger<ObsoleteRecurringJobCleaner> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 等 Web 启动完毕：不阻塞启动，且保证 DI 容器与 Hangfire 存储完全就绪
        try
        {
            await WaitForHostStartedAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(SweepInterval);

        do
        {
            Sweep(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    /// <summary>
    /// 扫描并清理一轮.
    /// </summary>
    /// <param name="stoppingToken">取消令牌.</param>
    private void Sweep(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var jobStorage = scope.ServiceProvider.GetRequiredService<JobStorage>();
            var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

            List<RecurringJobDto> jobs;
            using (var connection = jobStorage.GetConnection())
            {
                jobs = [.. connection.GetRecurringJobs()];
            }

            var removed = 0;
            foreach (var job in jobs)
            {
                if (stoppingToken.IsCancellationRequested || !IsObsolete(job))
                {
                    continue;
                }

                try
                {
                    // 幂等：多实例同时清理、或定义刚好被业务侧移除都不会报错
                    recurringJobManager.RemoveIfExists(job.Id);
                    removed++;

                    _logger.LogWarning(
                        "已清理无法解析任务体的遗留定时任务。任务Id={JobId}，创建时间={CreatedAt}，原因={Reason}",
                        job.Id,
                        job.CreatedAt?.ToString("O") ?? "unknown",
                        job.LoadException?.InnerException?.Message ?? job.LoadException?.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理遗留定时任务失败。任务Id={JobId}", job.Id);
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation("遗留定时任务清理完成，本轮移除 {Count} 个。", removed);
            }
        }
        catch (Exception ex)
        {
            // 清理属自愈动作，Redis/存储抖动不能影响宿主
            _logger.LogWarning(ex, "扫描遗留定时任务失败，等待下一轮重试。");
        }
    }

    /// <summary>
    /// 判定是否为「任务体已不存在」的遗留定义.
    /// </summary>
    /// <param name="job">recurring job 定义.</param>
    /// <returns>需要清理返回 true.</returns>
    private static bool IsObsolete(RecurringJobDto job)
    {
        // Removed=true 表示已在移除流程中，交给 Hangfire 自行收尾
        if (job.Removed || job.LoadException is null)
        {
            return false;
        }

        // CreatedAt 缺失按历史数据对待
        if (job.CreatedAt is not { } createdAt)
        {
            return true;
        }

        // 存储侧按 UTC ticks 落库、反序列化后多为 Unspecified，统一按 UTC 解释
        var createdAtUtc = createdAt.Kind == DateTimeKind.Local
            ? createdAt.ToUniversalTime()
            : createdAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
                : createdAt;

        // 宽限期内的定义不动，避免滚动发布期间旧实例误删新版本刚注册的任务
        return DateTime.UtcNow - createdAtUtc >= GracePeriod;
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // 避免阻塞 Web 启动，在 ASP.NET Core 启动完毕后才会启动此后台服务
    private Task<bool> WaitForHostStartedAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        _hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            tcs.TrySetResult(true);
        });

        cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled();
        });

        return tcs.Task;
    }
}
