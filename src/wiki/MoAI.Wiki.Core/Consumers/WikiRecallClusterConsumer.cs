using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.Extensions.Logging;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Consumers;

/// <summary>
/// 知识库检索经验聚类消费者：单队列串行（Qos=1），沉淀失败仅记日志并 Ack 放弃
/// （漏记一次沉淀仅损失一次复用机会，不重投避免毒消息堆积）.
/// </summary>
[Consumer(WikiRecallClusterDefaults.QueueName, Qos = 1)]
public class WikiRecallClusterConsumer : IConsumer<WikiRecallClusterMessage>
{
    private readonly WikiRecallClusterService _clusterService;
    private readonly ILogger<WikiRecallClusterConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiRecallClusterConsumer"/> class.
    /// </summary>
    /// <param name="clusterService">检索经验聚类领域服务.</param>
    /// <param name="logger">日志.</param>
    public WikiRecallClusterConsumer(WikiRecallClusterService clusterService, ILogger<WikiRecallClusterConsumer> logger)
    {
        _clusterService = clusterService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, WikiRecallClusterMessage message)
    {
        try
        {
            await _clusterService.UpsertAsync(message.WikiId, message.Query, message.Hits, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // 沉淀失败不重投：仅记日志
            _logger.LogWarning(ex, "检索经验聚类沉淀失败（已放弃）. WikiId={WikiId}", message.WikiId);
        }
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, WikiRecallClusterMessage message)
    {
        _logger.LogWarning(ex, "检索经验聚类消费异常. WikiId={WikiId}, RetryCount={RetryCount}", message.WikiId, retryCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, WikiRecallClusterMessage? message, Exception? ex)
    {
        _logger.LogWarning(ex, "检索经验聚类任务放弃. WikiId={WikiId}", message?.WikiId);
        return Task.FromResult(ConsumerState.Ack);
    }
}
