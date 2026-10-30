using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;

namespace MoAI.Timer.Services;

/// <summary>
/// 僵尸执行清理：宿主重启/进程挂死会遗留 Running 状态的执行记录（实际已无人在跑），
/// 启动即扫一轮、此后每 5 分钟扫描：开始时间超过 30 分钟仍 Running 的直接置为终态失败
/// （阈值大于单次尝试 10 分钟超时，不会误伤仍在新窗口内执行的记录）.
/// </summary>
public sealed class TeamTimerStaleExecutionSweeper : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TeamTimerStaleExecutionSweeper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerStaleExecutionSweeper"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">作用域工厂（DatabaseContext 为 scoped）.</param>
    /// <param name="logger">日志.</param>
    public TeamTimerStaleExecutionSweeper(IServiceScopeFactory serviceScopeFactory, ILogger<TeamTimerStaleExecutionSweeper> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 清理失败不终止服务，等待下一轮
                    _logger.LogWarning(ex, "定时器僵尸执行清理失败.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 宿主关停
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var threshold = DateTimeOffset.Now - StaleThreshold;
        var count = await databaseContext.TeamTimerExecutions
            .Where(x => x.Status == (int)TimerExecutionStatus.Running && x.StartTime < threshold)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, (int)TimerExecutionStatus.Failed)
                .SetProperty(x => x.ErrorMessage, "执行超时未恢复（宿主重启或长时间无响应），已自动标记失败.")
                .SetProperty(x => x.EndTime, DateTimeOffset.Now), cancellationToken);

        if (count > 0)
        {
            _logger.LogWarning("已将 {Count} 条超时未恢复的定时器执行记录标记为失败.", count);
        }
    }
}
