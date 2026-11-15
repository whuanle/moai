using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoAI.Subsystem.Client.Resources;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 心跳后台宿主：按配置间隔（默认跟随服务端建议 20s）上报实例在场，
/// 断线/停用/网络异常只记日志并继续下一轮，不阻塞宿主启动。
/// </summary>
public sealed class SubsysHeartbeatWorker : BackgroundService
{
    private static readonly TimeSpan FallbackInterval = TimeSpan.FromSeconds(20);

    private readonly SubsysResourceClient _resourceClient;
    private readonly MoaiSubsysClientOptions _options;
    private readonly ILogger<SubsysHeartbeatWorker> _logger;
    private readonly string _instanceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysHeartbeatWorker"/> class.
    /// </summary>
    /// <param name="resourceClient">资源客户端（心跳端点）。</param>
    /// <param name="options">客户端配置。</param>
    /// <param name="logger">日志。</param>
    public SubsysHeartbeatWorker(
        SubsysResourceClient resourceClient,
        IOptions<MoaiSubsysClientOptions> options,
        ILogger<SubsysHeartbeatWorker> logger)
    {
        _resourceClient = resourceClient;
        _options = options.Value;
        _logger = logger;
        _instanceId = _options.Heartbeat.ResolveInstanceId();
    }

    /// <summary>当前实例标识（多实例部署时每个进程唯一且稳定）.</summary>
    public string InstanceId => _instanceId;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _options.Heartbeat.IntervalSeconds > 0
            ? TimeSpan.FromSeconds(_options.Heartbeat.IntervalSeconds)
            : FallbackInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _resourceClient.HeartbeatAsync(_instanceId, _options.Heartbeat.Version, stoppingToken);
                if (_options.Heartbeat.IntervalSeconds <= 0 && response.RecommendedIntervalSeconds > 0)
                {
                    interval = TimeSpan.FromSeconds(response.RecommendedIntervalSeconds);
                }

                _logger.LogDebug(
                    "接入心跳成功. AppId={AppId}, InstanceId={InstanceId}, OnlineInstances={OnlineInstances}",
                    _options.AppId,
                    _instanceId,
                    response.OnlineInstances);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SubsysException ex)
            {
                _logger.LogWarning(ex, "接入心跳被拒绝，将按间隔重试. AppId={AppId}, StatusCode={StatusCode}, Code={Code}", _options.AppId, ex.StatusCode, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "接入心跳失败，将按间隔重试. AppId={AppId}", _options.AppId);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
