using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.Extensions.Logging;
using MoAI.Memory.Events;
using MoAI.Memory.Models;
using MoAI.Memory.Services;

namespace MoAI.Memory.Consumers;

/// <summary>
/// 长期记忆向量化模型迁移消费者：单任务串行（Qos=1），
/// 失败置任务失败后 Ack 放弃（旧向量原样保留，管理员修正后可重新发起，不重投避免毒消息堆积）.
/// </summary>
[Consumer(MemoryMigrationTask.QueueName, Qos = 1)]
public class MemoryModelMigrationConsumer : IConsumer<MemoryModelMigrationMessage>
{
    private readonly IMemoryModelMigrationService _migrationService;
    private readonly ILogger<MemoryModelMigrationConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryModelMigrationConsumer"/> class.
    /// </summary>
    /// <param name="migrationService">迁移执行器.</param>
    /// <param name="logger">日志.</param>
    public MemoryModelMigrationConsumer(IMemoryModelMigrationService migrationService, ILogger<MemoryModelMigrationConsumer> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, MemoryModelMigrationMessage message)
    {
        // 服务内部已捕获常规失败并置任务失败，这里兜底避免异常冒泡触发 MQ 重投循环
        try
        {
            await _migrationService.RunAsync(message.TaskId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记忆模型迁移消费异常. TaskId={TaskId}", message.TaskId);
            await _migrationService.MarkFailedAsync(message.TaskId, ex.Message, CancellationToken.None);
        }
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, MemoryModelMigrationMessage message)
    {
        _logger.LogError(ex, "记忆模型迁移消费异常. TaskId={TaskId}, RetryCount={RetryCount}", message.TaskId, retryCount);
        await _migrationService.MarkFailedAsync(message.TaskId, ex.Message, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, MemoryModelMigrationMessage? message, Exception? ex)
    {
        _logger.LogError(ex, "记忆模型迁移任务放弃. TaskId={TaskId}", message?.TaskId);
        if (message != null)
        {
            await _migrationService.MarkFailedAsync(message.TaskId, ex?.Message ?? "迁移任务失败。", CancellationToken.None);
        }

        return await Task.FromResult(ConsumerState.Ack);
    }
}
