using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.Extensions.Logging;
using MoAI.KnowledgeGraph.Consumers.Events;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Consumers;

/// <summary>
/// 知识图谱节点向量增量消费者：幂等处理（upsert 读最新状态），失败由 MQ 重投，重试耗尽 Ack 放弃.
/// </summary>
[Consumer("kg.node.embedding", Qos = 1)]
public class KgNodeEmbeddingConsumer : IConsumer<KgNodeEmbeddingDeltaMessage>
{
    private readonly IKgEmbeddingService _embeddingService;
    private readonly ILogger<KgNodeEmbeddingConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KgNodeEmbeddingConsumer"/> class.
    /// </summary>
    /// <param name="embeddingService">向量增量处理服务.</param>
    /// <param name="logger">日志.</param>
    public KgNodeEmbeddingConsumer(IKgEmbeddingService embeddingService, ILogger<KgNodeEmbeddingConsumer> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, KgNodeEmbeddingDeltaMessage message)
    {
        await _embeddingService.ProcessDeltaAsync(message, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, KgNodeEmbeddingDeltaMessage message)
    {
        _logger.LogError(ex, "kg embedding delta failed. KgId={KgId}, RetryCount={RetryCount}", message.KgId, retryCount);
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, KgNodeEmbeddingDeltaMessage? message, Exception? ex)
    {
        // 重试耗尽：Ack 放弃（检索侧向量缺失仅表现为不命中，可由后续节点操作再次触发），不阻塞队列
        _logger.LogError(ex, "kg embedding delta dropped after retries. KgId={KgId}", message?.KgId);
        return ConsumerState.Ack;
    }
}
