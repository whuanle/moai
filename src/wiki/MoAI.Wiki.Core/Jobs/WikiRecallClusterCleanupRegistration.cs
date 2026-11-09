using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Jobs;

/// <summary>
/// 启动后注册检索经验聚类清理定时任务（每日 UTC 0 点）.
/// </summary>
internal sealed class WikiRecallClusterCleanupRegistration : BackgroundService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<WikiRecallClusterCleanupRegistration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiRecallClusterCleanupRegistration"/> class.
    /// </summary>
    /// <param name="recurringJobManager">Hangfire 周期任务管理器.</param>
    /// <param name="hostApplicationLifetime">宿主生命周期.</param>
    /// <param name="logger">日志.</param>
    public WikiRecallClusterCleanupRegistration(
        IRecurringJobManager recurringJobManager,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<WikiRecallClusterCleanupRegistration> logger)
    {
        _recurringJobManager = recurringJobManager;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForHostStartedAsync(stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _recurringJobManager.AddOrUpdate<WikiRecallClusterCleanupJob>(
                WikiRecallClusterDefaults.CleanupJobKey,
                task => task.InvokeAsync(),
                cronExpression: WikiRecallClusterDefaults.CleanupCron,
                options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
#pragma warning disable CA1031 // 注册失败不影响应用启动
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册检索经验聚类清理定时任务失败.");
        }
#pragma warning restore CA1031
    }

    private Task WaitForHostStartedAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        _hostApplicationLifetime.ApplicationStarted.Register(() => tcs.TrySetResult(true));
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }
}
