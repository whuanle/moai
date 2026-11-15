using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.Subsystem.Client.Events;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 事件客户端宿主：随应用启动/停止自动管理 <see cref="ISubsysEventClient"/> 连接循环，
/// 接入方无需在 <c>Program.cs</c> 手工 Start/Stop。
/// </summary>
public sealed class SubsysEventWorker : BackgroundService
{
    private readonly ISubsysEventClient _client;
    private readonly ILogger<SubsysEventWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysEventWorker"/> class.
    /// </summary>
    /// <param name="client">事件客户端.</param>
    /// <param name="logger">日志.</param>
    public SubsysEventWorker(ISubsysEventClient client, ILogger<SubsysEventWorker> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _client.StartAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接入事件后台连接异常退出.");
        }
        finally
        {
            await _client.StopAsync(CancellationToken.None);
        }
    }
}
