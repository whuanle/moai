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
/// 知识库文档工作流消费者：按任务数据执行 AI 切割 → 元数据生成 → 向量化（各步骤可选）.
/// </summary>
[Consumer("wiki.document.embedding", Qos = 1)]
public class EmbeddingDocumentConsumer : IConsumer<EmbeddingDocumentTaskMessage>
{
    private const string ProcessingMessage = "任务处理中";
    private const string SuccessfulMessage = "任务已完成";

    private readonly DatabaseContext _databaseContext;
    private readonly IWikiWorkflowProcessor _workflowProcessor;
    private readonly ILogger<EmbeddingDocumentConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="workflowProcessor">文档工作流处理器.</param>
    /// <param name="logger">日志.</param>
    public EmbeddingDocumentConsumer(DatabaseContext databaseContext, IWikiWorkflowProcessor workflowProcessor, ILogger<EmbeddingDocumentConsumer> logger)
    {
        _databaseContext = databaseContext;
        _workflowProcessor = workflowProcessor;
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

        if (data == null || data.WikiId <= 0 || data.DocumentId <= 0
            || (!data.IsEmbedSourceText && !data.IsEmbedMetadata && data.MetadataModelId == Guid.Empty && data.AiPartitionModelId == Guid.Empty))
        {
            await MarkTaskFailedIfPendingAsync(message.TaskId, "任务数据无效：缺少有效的知识库、文档或处理步骤配置。");
            return;
        }

        _logger.LogInformation(
            "Starting document workflow task. TaskId={TaskId}, WikiId={WikiId}, DocumentId={DocumentId}, AiPartition={AiPartition}, GenerateMetadata={GenerateMetadata}",
            message.TaskId,
            data.WikiId,
            data.DocumentId,
            data.AiPartitionModelId != Guid.Empty,
            data.MetadataModelId != Guid.Empty);

        try
        {
            await _workflowProcessor.ProcessAsync(data, CancellationToken.None);

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
