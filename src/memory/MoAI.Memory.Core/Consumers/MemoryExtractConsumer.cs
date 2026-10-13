using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.Extensions.Logging;
using MoAI.Memory.Events;
using MoAI.Memory.Services;

namespace MoAI.Memory.Consumers;

/// <summary>
/// 会话记忆提取消费者：幂等处理（合并决策天然幂等 + Redis 会话锁串行化），
/// 失败记录日志后 Ack 放弃（记忆为尽力而为，后续轮次会再次触发，不重投避免毒消息堆积）.
/// </summary>
[Consumer("app.memory.extract", Qos = 1)]
public class MemoryExtractConsumer : IConsumer<MemoryExtractMessage>
{
    private readonly IMemoryService _memoryService;
    private readonly ILogger<MemoryExtractConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryExtractConsumer"/> class.
    /// </summary>
    /// <param name="memoryService">记忆领域服务.</param>
    /// <param name="logger">日志.</param>
    public MemoryExtractConsumer(IMemoryService memoryService, ILogger<MemoryExtractConsumer> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, MemoryExtractMessage message)
    {
        try
        {
            await _memoryService.ExtractAndConsolidateAsync(message.SessionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // 双保险：服务内部已捕获常规失败，这里兜底避免异常冒泡触发 MQ 重投循环
            _logger.LogWarning(ex, "会话 {SessionId} 记忆提取消费失败.", message.SessionId);
        }
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, MemoryExtractMessage message)
    {
        _logger.LogError(ex, "记忆提取消费异常. SessionId={SessionId}, RetryCount={RetryCount}", message.SessionId, retryCount);
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, MemoryExtractMessage? message, Exception? ex)
    {
        _logger.LogError(ex, "记忆提取任务放弃. SessionId={SessionId}", message?.SessionId);
        return await Task.FromResult(ConsumerState.Ack);
    }
}
