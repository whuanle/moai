using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Consumers;

/// <summary>
/// 知识库文档向量化消费者：复用已提取内容 + 已切割切片，执行 可选元数据生成 → 向量化.
/// </summary>
[Consumer("wiki.document.embedding", Qos = 1)]
public class EmbeddingDocumentConsumer : IConsumer<EmbeddingDocumentTaskMessage>
{
    private const string ProcessingMessage = "任务处理中";
    private const string SuccessfulMessage = "任务已完成";

    private readonly DatabaseContext _databaseContext;
    private readonly IWikiEmbeddingProcessor _embeddingProcessor;
    private readonly ILogger<EmbeddingDocumentConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="embeddingProcessor">向量化流水线服务.</param>
    /// <param name="logger">日志.</param>
    public EmbeddingDocumentConsumer(DatabaseContext databaseContext, IWikiEmbeddingProcessor embeddingProcessor, ILogger<EmbeddingDocumentConsumer> logger)
    {
        _databaseContext = databaseContext;
        _embeddingProcessor = embeddingProcessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMessage message)
    {
        var now = DateTimeOffset.UtcNow;
        var affectedRows = await _databaseContext.WorkerTasks
            .Where(x => x.Id == message.TaskId && x.IsDeleted == 0 && x.State == (int)MoAI.Infra.Models.WorkerState.Wait)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.State, (int)MoAI.Infra.Models.WorkerState.Processing)
                    .SetProperty(x => x.Message, ProcessingMessage)
                    .SetProperty(x => x.UpdateTime, now),
                CancellationToken.None);

        if (affectedRows != 1)
        {
            _logger.LogInformation("Skip embedding task because claim failed. TaskId={TaskId}, AffectedRows={AffectedRows}", message.TaskId, affectedRows);
            return;
        }

        var taskDataJson = await _databaseContext.WorkerTasks
            .Where(x => x.Id == message.TaskId && x.IsDeleted == 0)
            .Select(x => x.Data)
            .FirstOrDefaultAsync(CancellationToken.None);

        if (string.IsNullOrWhiteSpace(taskDataJson))
        {
            await MarkTaskFailedIfPendingAsync(message.TaskId, "任务数据无效：任务数据为空。");
            return;
        }

        WikiDocumentEmbeddingTaskData? data;
        try
        {
            data = JsonSerializer.Deserialize<WikiDocumentEmbeddingTaskData>(taskDataJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid worker task data json. TaskId={TaskId}", message.TaskId);
            await MarkTaskFailedIfPendingAsync(message.TaskId, "任务数据无效：JSON 解析失败。");
            return;
        }

        if (data == null || data.WikiId <= 0 || data.DocumentId <= 0 || (!data.IsEmbedSourceText && !data.IsEmbedMetadata))
        {
            await MarkTaskFailedIfPendingAsync(message.TaskId, "任务数据无效：缺少有效的知识库、文档或向量化选项配置。");
            return;
        }

        _logger.LogInformation(
            "Starting document embedding. TaskId={TaskId}, WikiId={WikiId}, DocumentId={DocumentId}",
            message.TaskId,
            data.WikiId,
            data.DocumentId);

        try
        {
            await _embeddingProcessor.ProcessAsync(
                data.WikiId,
                data.DocumentId,
                data.IsEmbedSourceText,
                data.IsEmbedMetadata,
                CancellationToken.None);

            await _databaseContext.WorkerTasks
                .Where(x => x.Id == message.TaskId && x.IsDeleted == 0 && x.State == (int)MoAI.Infra.Models.WorkerState.Processing)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.State, (int)MoAI.Infra.Models.WorkerState.Successful)
                        .SetProperty(x => x.Message, SuccessfulMessage)
                        .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow),
                    CancellationToken.None);

            _logger.LogInformation("Document embedding completed. TaskId={TaskId}, WikiId={WikiId}, DocumentId={DocumentId}", message.TaskId, data.WikiId, data.DocumentId);
        }
        catch (Exception ex)
        {
            await MarkTaskFailedBestEffortAsync(message.TaskId, ex.Message, ex, "execute");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, EmbeddingDocumentTaskMessage message)
    {
        await MarkTaskFailedBestEffortAsync(message.TaskId, ex.Message, ex, "retry");
        _logger.LogError(ex, "Document embedding failed. TaskId={TaskId}, RetryCount={RetryCount}", message.TaskId, retryCount);
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMessage? message, Exception? ex)
    {
        if (message != null)
        {
            await MarkTaskFailedBestEffortAsync(message.TaskId, ex?.Message, ex, "fallback");
        }

        _logger.LogError(ex, "Document embedding moved to dead letter. TaskId={TaskId}", message?.TaskId);
        return ConsumerState.Ack;
    }

    private async Task MarkTaskFailedBestEffortAsync(Guid taskId, string? message, Exception? sourceException, string stage)
    {
        try
        {
            await MarkTaskFailedIfPendingAsync(taskId, message);
        }
        catch (Exception markFailedEx)
        {
            _logger.LogError(
                markFailedEx,
                "Failed to persist failed state in {Stage}. TaskId={TaskId}, OriginalError={OriginalError}",
                stage,
                taskId,
                sourceException?.Message);
        }
    }

    private async Task MarkTaskFailedIfPendingAsync(Guid taskId, string? message)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "任务执行失败。"
            : message.Trim();

        await _databaseContext.WorkerTasks
            .Where(
                x => x.Id == taskId
                    && x.IsDeleted == 0
                    && (x.State == (int)MoAI.Infra.Models.WorkerState.Wait || x.State == (int)MoAI.Infra.Models.WorkerState.Processing))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.State, (int)MoAI.Infra.Models.WorkerState.Failed)
                    .SetProperty(x => x.Message, normalizedMessage)
                    .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow),
                CancellationToken.None);
    }
}
