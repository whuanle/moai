#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MoAI.AI.MemoryDb;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Embedding.Models;
using MoAI.Wiki.Models;
using MoAIDocument.Core.Consumers.Events;
using System.Threading;

namespace MoAIDocument.Core.Consumers;

/// <summary>
/// 文档向量化.
/// </summary>
[Consumer("embedding_document", Qos = 10)]
public class EmbeddingDocumentCommandConsumer : IConsumer<EmbeddingDocumentTaskMesage>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly ILogger<EmbeddingDocumentCommandConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="mediator"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    /// <param name="aiClientBuilder"></param>
    public EmbeddingDocumentCommandConsumer(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator, IServiceProvider serviceProvider, ILogger<EmbeddingDocumentCommandConsumer> logger, IAiClientBuilder aiClientBuilder)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _mediator = mediator;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _aiClientBuilder = aiClientBuilder;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMesage message)
    {
        // 1，前置检查
        var workerTask = await _databaseContext.WorkerTasks.AsNoTracking()
             .FirstOrDefaultAsync(x => x.Id == message.TaskId);

        // 不需要处理或有其它线程在执行
        if (workerTask == null || workerTask.State > (int)WorkerState.Processing)
        {
            return;
        }

        var embddingConfig = workerTask.Data.JsonToObject<WikiDocumentEmbeddingConfig>()!;

        var documenEntity = await _databaseContext.WikiDocuments.Where(x => x.Id == message.DocumentId).FirstOrDefaultAsync();

        if (documenEntity == null)
        {
            await SetStateAsync(message.TaskId, WorkerState.Successful);
            return;
        }

        await SetStateAsync(message.TaskId, WorkerState.Processing);

        var (wikiConfig, aiEndpoint) = await GetWikiConfigAsync(documenEntity.WikiId);

        if (wikiConfig == null || aiEndpoint == null)
        {
            await SetStateAsync(message.TaskId, WorkerState.Failed, "知识库未配置向量化模型");
            return;
        }

        // 2，读取需要向量化的文本
        // 读取需要向量化的文本
        var chunks = await _databaseContext.WikiDocumentChunkContentPreviews.Where(x => x.DocumentId == documenEntity.Id)
            .ToListAsync();

        if (chunks.Count == 0)
        {
            await SetStateAsync(message.TaskId, WorkerState.Successful);
            return;
        }

        var chunkIds = chunks.Select(x => x.Id).ToArray();
        var derivedContents = await _databaseContext.WikiDocumentChunkDerivativePreviews
            .Where(x => chunkIds.Contains(x.ChunkId))
            .ToListAsync();

        // 3，生成快照存储
        var embeddingEntities = new List<WikiDocumentChunkEmbeddingEntity>();
        foreach (var chunk in chunks)
        {
            var item = new WikiDocumentChunkEmbeddingEntity
            {
                Id = Guid.CreateVersion7(),
                ChunkId = default(Guid),
                WikiId = chunk.WikiId,
                DocumentId = chunk.DocumentId,
                DerivativeContent = chunk.SliceContent,
                DerivativeType = 0,
                IsEmbedding = embddingConfig.IsEmbedSourceText
            };

            embeddingEntities.Add(item);

            var deriveds = derivedContents.Where(x => x.ChunkId == chunk.Id);
            foreach (var derived in deriveds)
            {
                var derivedItem = new WikiDocumentChunkEmbeddingEntity
                {
                    Id = Guid.CreateVersion7(),
                    ChunkId = item.Id,
                    WikiId = chunk.WikiId,
                    DocumentId = chunk.DocumentId,
                    DerivativeContent = derived.DerivativeContent,
                    DerivativeType = (int)derived.DerivativeType,
                    IsEmbedding = true
                };
                embeddingEntities.Add(derivedItem);
            }
        }

        _databaseContext.WikiDocumentChunkEmbeddings.AddRange(embeddingEntities);
        await _databaseContext.SaveChangesAsync();

        // 4，开始向量化
        var wikiIndex = message.WikiId.ToString();
        var documentIndex = message.DocumentId.ToString();

        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiEndpoint, wikiConfig.EmbeddingDimensions);
        var memoryDb = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        await CheckIndexIsExistAsync(memoryDb, wikiIndex, wikiConfig.EmbeddingDimensions);

        // 删除这个文档以前的向量
        await _mediator.Send(new ClearWikiDocumentEmbeddingCommand
        {
            WikiId = message.WikiId,
            DocumentId = message.DocumentId
        });

        // 逐个向量化
        var parallelism = Math.Max(1, embddingConfig.ThreadCount);
        using var embeddingSemaphore = new SemaphoreSlim(parallelism, parallelism);
        using var upsertSemaphore = new SemaphoreSlim(1, 1);
        var embeddingTasks = new List<Task>();

        foreach (var item in embeddingEntities)
        {
            if (item.IsEmbedding == false)
            {
                continue;
            }

            embeddingTasks.Add(ProcessEmbeddingAsync(item));
        }

        if (embeddingTasks.Count > 0)
        {
            await Task.WhenAll(embeddingTasks);
        }

        async Task ProcessEmbeddingAsync(WikiDocumentChunkEmbeddingEntity item)
        {
            await embeddingSemaphore.WaitAsync();
            try
            {
                var embedding = await textEmbeddingGenerator.GenerateEmbeddingAsync(item.DerivativeContent);

                var record = new Microsoft.KernelMemory.MemoryStorage.MemoryRecord()
                {
                    Id = item.Id.ToString("N"),

                    // 附加属性
                    Payload = new Dictionary<string, object>
                    {
                        { "file",  documenEntity.FileName },
                        { "text", item.DerivativeContent },
                        { "vector_provider", aiEndpoint.Provider },
                        { "last_update", DateTimeOffset.Now.ToJsonString() }
                    },

                    // tags 是 筛选条件
                    Tags = new TagCollection()
                    {
                        { "document_id", documentIndex },
                        { "chunk_id", item.ChunkId.ToString() },
                        { "embedding_id", item.Id.ToString("N") },
                        { "file_id", documenEntity.ObjectKey },
                        { "file_type", documenEntity.FileType },
                    },

                    Vector = embedding
                };

                await upsertSemaphore.WaitAsync();
                try
                {
                    await memoryDb.UpsertAsync(
                        index: message.WikiId.ToString(),
                        record: record,
                        cancellationToken: CancellationToken.None);
                }
                finally
                {
                    upsertSemaphore.Release();
                }
            }
            finally
            {
                embeddingSemaphore.Release();
            }
        }

        await SetStateAsync(message.TaskId, WorkerState.Successful);

        documenEntity.IsEmbedding = true;
        _databaseContext.WikiDocuments.Update(documenEntity);

        await _databaseContext.Wikis.Where(x => x.Id == message.WikiId).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, true));
        await _databaseContext.SaveChangesAsync();

        await _databaseContext.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, EmbeddingDocumentTaskMesage message)
    {
        if (message != null)
        {
            await SetExceptionStateAsync(message.TaskId, WorkerState.Failed, ex);
        }

        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMesage? message, Exception? ex)
    {
        if (message != null)
        {
            await SetExceptionStateAsync(message.TaskId, WorkerState.Failed, ex);
        }

        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
        return ConsumerState.Ack;
    }

    private static async Task CheckIndexIsExistAsync(Microsoft.KernelMemory.MemoryStorage.IMemoryDb memoryDb, string index, int vectorSize)
    {
        var indexs = await memoryDb.GetIndexesAsync();

        if (!indexs.Contains(index))
        {
            await memoryDb.CreateIndexAsync(index, vectorSize);
        }
    }

    private async Task<(EmbeddingConfig? WikiConfig, AiEndpoint? AiEndpoint)> GetWikiConfigAsync(int wikiId)
    {
        var result = await _databaseContext.Wikis
        .Where(x => x.Id == wikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiConfig = new EmbeddingConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingModelId = a.EmbeddingModelId,
            },
            AiEndpoint = new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                Provider = x.AiProvider.JsonToObject<AiProvider>(),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput,
                Key = x.Key,
            }
        }).FirstOrDefaultAsync();

        return (result?.WikiConfig, result?.AiEndpoint);
    }

    private async Task SetExceptionStateAsync(Guid taskId, WorkerState state, Exception? ex = null)
    {
        _logger.LogError(ex, "Task processing failed.");
        await SetStateAsync(taskId, state, ex?.Message);
    }

    private async Task SetStateAsync(Guid taskId, WorkerState state, string? message = null)
    {
        // 设置之前先检查状态
        var workerTask = await _databaseContext.WorkerTasks
             .FirstOrDefaultAsync(x => x.Id == taskId);

        // 不需要处理或有其它线程在执行
        if (workerTask == null || workerTask.State > (int)WorkerState.Processing)
        {
            throw new BusinessException("当前任务已结束");
        }

        workerTask.State = (int)state;
        if (!string.IsNullOrEmpty(message))
        {
            workerTask.Message = message;
        }
        else
        {
            workerTask.Message = state.ToJsonString();
        }

        _databaseContext.WorkerTasks.Update(workerTask);
        await _databaseContext.SaveChangesAsync();
    }
}
