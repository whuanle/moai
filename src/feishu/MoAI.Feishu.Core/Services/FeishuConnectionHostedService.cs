using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书长连接启动宿主服务：启动租约循环参与领导者选举，持租约的实例建立全部飞书长连接；
/// 初始化失败只记录日志，不阻断宿主启动.
/// </summary>
public sealed class FeishuConnectionHostedService : IHostedService
{
    private readonly FeishuConnectionManager _connectionManager;
    private readonly ILogger<FeishuConnectionHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuConnectionHostedService"/> class.
    /// </summary>
    /// <param name="connectionManager">长连接管理器.</param>
    /// <param name="logger">日志.</param>
    public FeishuConnectionHostedService(FeishuConnectionManager connectionManager, ILogger<FeishuConnectionHostedService> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connectionManager.StartLease();
        }
        catch (Exception ex)
        {
            // Redis 不可用等情况下降级为无长连接，不阻断宿主启动
            _logger.LogError(ex, "飞书长连接租约启动失败，本实例将不接收飞书事件.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _connectionManager.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "飞书长连接停止异常.");
        }
    }
}
