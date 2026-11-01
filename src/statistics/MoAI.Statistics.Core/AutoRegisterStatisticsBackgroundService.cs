using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.Statistics.Services;

namespace MoAI.Statistics;

/// <summary>
/// 注册资源使用统计批量落库定时任务.
/// </summary>
internal class AutoRegisterStatisticsBackgroundService : BackgroundService
{
    private readonly ILogger<AutoRegisterStatisticsBackgroundService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoRegisterStatisticsBackgroundService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="hostApplicationLifetime"></param>
    /// <param name="serviceScopeFactory"></param>
    public AutoRegisterStatisticsBackgroundService(ILogger<AutoRegisterStatisticsBackgroundService> logger, IHostApplicationLifetime hostApplicationLifetime, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var applicationStartedTask = WaitForHostStartedAsync(stoppingToken);
        await applicationStartedTask;

        // web 取消启动
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();

        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        // 每分钟批量落库一次资源使用统计
        recurringJobManager.AddOrUpdate<ResourceUsageFlushJobHandler>(
            "resource-usage-flush",
            task => task.InvokeAsync(),
            cronExpression: "0 * * * * ?",
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
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
