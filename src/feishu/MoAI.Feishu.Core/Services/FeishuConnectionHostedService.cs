using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书长连接启动宿主服务，应用启动时为全部启用的飞书应用建立长连接；初始化失败只记录日志，不阻断宿主启动.
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
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _connectionManager.InitializeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // 存量库未建 feishu 表等情况下降级为无长连接，不阻断启动
            _logger.LogError(ex, "飞书长连接初始化失败，本实例将不接收飞书事件.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _connectionManager.StopAllAsync(cancellationToken);
    }
}
