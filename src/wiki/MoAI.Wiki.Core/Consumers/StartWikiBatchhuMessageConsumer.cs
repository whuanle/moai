#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.DocumentEmbedding.Commands;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Models;

namespace MoAIDocument.Core.Consumers;

/// <summary>
/// 批处理任务只能一个个来.
/// </summary>
[Consumer("batch_document", Qos = 1)]
public class StartWikiBatchhuMessageConsumer : IConsumer<StartWikiBatchhuMessage>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly ILogger<StartWikiBatchhuMessageConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWikiBatchhuMessageConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="logger"></param>
    /// <param name="mediator"></param>
    public StartWikiBatchhuMessageConsumer(DatabaseContext databaseContext, ILogger<StartWikiBatchhuMessageConsumer> logger, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _logger = logger;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, StartWikiBatchhuMessage message)
    {
        if (message.Command == null || message.Command.DocumentIds == null || message.Command.DocumentIds.Count == 0)
        {
            await SetStateAsync(message!.TaskId, WorkerState.Successful);
            return;
        }

        foreach (var documentId in message.Command.DocumentIds.ToHashSet())
        {
            _databaseContext.ChangeTracker.Clear();

            var (isBreak, _) = await SetStateAsync(message.TaskId, WorkerState.Processing, "正在处理文档");
            if (isBreak)
            {
                return;
            }

            try
            {
                var existDocument = await _databaseContext.WikiDocuments
                    .AnyAsync(x => x.Id == documentId && x.WikiId == message.WikiId);

                if (!existDocument)
                {
                    continue;
                }

                // 切割
                if (message.Command.Partion != null)
                {
                    await _mediator.Send(new WikiDocumentTextPartitionPreviewCommand
                    {
                        WikiId = message.WikiId,
                        DocumentId = documentId,
                        ChunkHeader = message.Command.Partion.ChunkHeader,
                        MaxTokensPerChunk = message.Command.Partion.MaxTokensPerChunk,
                        Overlap = message.Command.Partion.Overlap,
                    });
                }
                else if (message.Command.AiPartion != null)
                {
                    await _mediator.Send(new WikiDocumentAiTextPartionCommand
                    {
                        WikiId = message.WikiId,
                        DocumentId = documentId,
                        PromptTemplate = message.Command.AiPartion.PromptTemplate,
                        AiModelId = message.Command.AiPartion.AiModelId,
                    });
                }
                else
                {
                    continue;
                }

                List<AddWikiDocumentDerivativeItem> derivativeItems = new();

                // 生成元数据
                foreach (var preprocessStrategyType in message.Command.PreprocessStrategyType)
                {
                    var chunks = await _mediator.Send(new WikiDocumentChunkAiGenerationCommand
                    {
                        AiModelId = message.Command.PreprocessStrategyAiModel,
                        WikiId = message.WikiId,
                        PreprocessStrategyType = preprocessStrategyType,
                        Chunks = await _databaseContext.WikiDocumentChunkContentPreviews.AsNoTracking()
                            .Where(x => x.DocumentId == documentId && x.WikiId == message.WikiId)
                            .Select(x => new KeyValue<long, string>(x.Id, x.SliceContent))
                            .ToListAsync()
                    });

                    foreach (var metadata in chunks.Items)
                    {
                        foreach (var metadataItem in metadata.Value.Metadata)
                        {
                            derivativeItems.Add(new AddWikiDocumentDerivativeItem
                            {
                                ChunkId = metadata.Key,
                                DerivativeType = metadataItem.Key.JsonToObject<ParagrahProcessorMetadataType>(),
                                DerivativeContent = metadataItem.Value,
                            });
                        }
                    }
                }

                if (derivativeItems.Count > 0)
                {
                    await _mediator.Send(new AddWikiDocumentChunkDerivativeCommand
                    {
                        WikiId = message.WikiId,
                        DocumentId = documentId,
                        Derivatives = derivativeItems
                    });
                }

                if (message.Command.IsEmbedding)
                {
                    await _mediator.Send(new EmbeddingDocumentCommand
                    {
                        WikiId = message.WikiId,
                        DocumentId = documentId,
                        IsEmbedSourceText = message.Command.IsEmbedSourceText,
                        ThreadCount = message.Command.ThreadCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process error {DocumentId}", documentId);
            }
        }

        await SetStateAsync(message.TaskId, WorkerState.Successful, "爬取完成");
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWikiBatchhuMessage message)
    {
        await SetExceptionStateAsync(message!.TaskId, WorkerState.Failed, ex);
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWikiBatchhuMessage? message, Exception? ex)
    {
        return Task.FromResult(ConsumerState.Ack);
    }

    private async Task<(bool IsBreak, WorkerState WorkerState)> SetExceptionStateAsync(Guid taskId, WorkerState state, Exception? ex = null)
    {
        _logger.LogError(ex, "Task processing failed.");
        return await SetStateAsync(taskId, state, ex?.Message);
    }

    private async Task<(bool IsBreak, WorkerState WorkerState)> SetStateAsync(Guid taskId, WorkerState state, string? message = null)
    {
        _databaseContext.ChangeTracker.Clear();

        // 设置之前先检查状态
        var workerEntity = await _databaseContext.WorkerTasks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == taskId);

        if (workerEntity == null)
        {
            return (true, WorkerState.Cancal);
        }

        // 不需要处理或有其它线程在执行
        if (workerEntity.State > (int)WorkerState.Processing)
        {
            return (true, WorkerState.Cancal);
        }

        workerEntity.State = (int)state;
        if (!string.IsNullOrEmpty(message))
        {
            workerEntity.Message = message;
        }
        else
        {
            workerEntity.Message = state.ToJsonString();
        }

        var entityState = _databaseContext.WorkerTasks.Update(workerEntity);
        await _databaseContext.SaveChangesAsync();
        _databaseContext.ChangeTracker.Clear();

        return (false, state);
    }
}
