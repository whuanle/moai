using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.Hangfire.Services;

namespace MoAI.Hangfire;

/// <summary>
/// 注册定时任务
/// </summary>
internal class AutoRegisterHangfireBackgroundService : BackgroundService
{
    private readonly ILogger<AutoRegisterHangfireBackgroundService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoRegisterHangfireBackgroundService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="hostApplicationLifetime"></param>
    /// <param name="serviceScopeFactory"></param>
    public AutoRegisterHangfireBackgroundService(ILogger<AutoRegisterHangfireBackgroundService> logger, IHostApplicationLifetime hostApplicationLifetime, IServiceScopeFactory serviceScopeFactory)
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

        // 每分钟执行一次计数器任务
        recurringJobManager.AddOrUpdate<CounterActivatorJobHandler>(
            "counter",
            task => task.InvokeAsync(),
            cronExpression: "* * * * ? *",
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