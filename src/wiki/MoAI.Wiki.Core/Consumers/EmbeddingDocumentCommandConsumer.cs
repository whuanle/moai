//#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

//using Maomi.MQ;
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Microsoft.KernelMemory;
//using Microsoft.KernelMemory.Chunkers;
//using Microsoft.KernelMemory.Configuration;
//using Microsoft.SemanticKernel.Memory;
//using MoAI.AI.MemoryDb;
//using MoAI.AI.Models;
//using MoAI.AiModel.Models;
//using MoAI.AiModel.Services;
//using MoAI.Database;
//using MoAI.Infra;
//using MoAI.Infra.Exceptions;
//using MoAI.Infra.Extensions;
//using MoAI.Storage.Queries;
//using MoAI.Wiki.Models;
//using MoAIDocument.Core.Consumers.Events;

//namespace MoAIDocument.Core.Consumers;

///// <summary>
///// 文档向量化.
///// </summary>
//[Consumer("embedding_document", Qos = 10)]
//public class EmbeddingDocumentCommandConsumer : IConsumer<EmbeddingDocumentTaskMesage>
//{
//    private readonly DatabaseContext _databaseContext;
//    private readonly SystemOptions _systemOptions;
//    private readonly IMediator _mediator;
//    private readonly IServiceProvider _serviceProvider;
//    private readonly IAiClientBuilder _aiClientBuilder;
//    private readonly ILogger<EmbeddingDocumentCommandConsumer> _logger;

//    /// <summary>
//    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandConsumer"/> class.
//    /// </summary>
//    /// <param name="databaseContext"></param>
//    /// <param name="systemOptions"></param>
//    /// <param name="mediator"></param>
//    /// <param name="serviceProvider"></param>
//    /// <param name="logger"></param>
//    public EmbeddingDocumentCommandConsumer(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator, IServiceProvider serviceProvider, ILogger<EmbeddingDocumentCommandConsumer> logger, IAiClientBuilder aiClientBuilder)
//    {
//        _databaseContext = databaseContext;
//        _systemOptions = systemOptions;
//        _mediator = mediator;
//        _serviceProvider = serviceProvider;
//        _logger = logger;
//        _aiClientBuilder = aiClientBuilder;
//    }

//    /// <inheritdoc/>
//    public async Task ExecuteAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMesage message)
//    {
//        var workerTask = await _databaseContext.WorkerTasks.AsNoTracking()
//             .FirstOrDefaultAsync(x => x.Id == message.TaskId);

//        // 不需要处理或有其它线程在执行
//        if (workerTask == null || workerTask.State >= (int)WorkerState.Processing)
//        {
//            return;
//        }

//        var documenEntity = await _databaseContext.WikiDocuments.Where(x => x.Id == message.DocumentId).FirstOrDefaultAsync();

//        if (documenEntity == null)
//        {
//            await SetStateAsync(message.TaskId, WorkerState.Successful);
//            return;
//        }

//        var plainTextChunkerOptions = documenEntity.SliceConfig.JsonToObject<PlainTextChunkerOptions>();

//        await SetStateAsync(message.TaskId, WorkerState.Processing);

//        var filePath = await _mediator.Send(new QueryFileLocalPathCommand
//        {
//            ObjectKey = documenEntity.ObjectKey
//        });

//        var (wikiConfig, aiEndpoint) = await GetWikiConfigAsync(documenEntity.WikiId);

//        if (wikiConfig == null || aiEndpoint == null)
//        {
//            await SetStateAsync(message.TaskId, WorkerState.Failed, "知识库未配置向量化模型");
//            return;
//        }

//        // 读取需要向量化的文本
//        var chunks = await _databaseContext.WikiDocumentChunkContentPreviews.Where(x => x.DocumentId == documenEntity.Id)
//            .ToListAsync();

//        if (chunks.Count == 0)
//        {
//            await SetStateAsync(message.TaskId, WorkerState.Successful);
//            return;
//        }

//        var chunkIds = chunks.Select(x => x.Id).ToArray();
//        var derivedContents = await _databaseContext.WikiDocumentChunkDerivativePreviews
//            .Where(x => chunkIds.Contains(x.SliceId))
//            .ToListAsync();

//        // 构建客户端
//        var memoryBuilder = new KernelMemoryBuilder()
//            .WithoutTextGenerator();

//        //.WithSimpleFileStorage(Path.GetTempPath());

//        var textEmbeddingGeneration = _serviceProvider.GetKeyedService<ITextEmbeddingGeneration>(aiEndpoint.Provider);

//        if (textEmbeddingGeneration == null)
//        {
//            textEmbeddingGeneration = _serviceProvider.GetKeyedService<ITextEmbeddingGeneration>(AiProvider.Custom);
//        }

//        if (textEmbeddingGeneration == null)
//        {
//            await SetStateAsync(message.TaskId, WorkerState.Failed, $"不支持的模型供应商 {aiEndpoint.Provider}");
//            return;
//        }

//        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiEndpoint);

//        var memoryDb = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

//        if (memoryDb == null)
//        {
//            await SetStateAsync(message.TaskId, WorkerState.Failed, "不支持的数据库类型，请联系管理员");
//            return;
//        }

//        // 删除这个文档以前的向量
//        var records = memoryDb.GetListAsync(
//            index: message.WikiId.ToString(),
//            limit: -1,
//            filters: [MemoryFilters.ByDocument(message.DocumentId.ToString())],
//            cancellationToken: CancellationToken.None);

//        await foreach (var record in records.WithCancellation(CancellationToken.None).ConfigureAwait(false))
//        {
//            await memoryDb.DeleteAsync(index: message.WikiId.ToString(), record, cancellationToken: CancellationToken.None).ConfigureAwait(false);
//        }

//        // 开始生成新的向量

//        textEmbeddingGeneration.Configure(memoryBuilder, aiEndpoint, wikiConfig);
//        memoryDb.Configure(memoryBuilder, _systemOptions.Wiki.ConnectionString);

//        var memoryClient = memoryBuilder.WithoutTextGenerator()
//            .WithCustomTextPartitioningOptions(
//            new TextPartitioningOptions
//            {
//                MaxTokensPerParagraph = plainTextChunkerOptions!.MaxTokensPerChunk,
//                OverlappingTokens = plainTextChunkerOptions.Overlap,
//            })
//            .Build();

//        // 先删除历史记录
//        await memoryClient.DeleteDocumentAsync(workerTask.DocumentId.ToString(), index: workerTask.WikiId.ToString());

//        var docs = new Microsoft.KernelMemory.Document()
//        {
//            Id = workerTask.DocumentId.ToString(),
//        };

//        // 自行读取流以便自定义文件名称
//        using var documentStream = new FileStream(filePath.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
//        docs.Files.AddStream(documenEntity.FileName, documentStream);
//        docs.AddTag("wikiId", documenEntity.WikiId.ToString());
//        docs.AddTag("fileId", documenEntity.FileId.ToString());

//        try
//        {
//            var taskId = await memoryClient.ImportDocumentAsync(docs, index: workerTask.WikiId.ToString());
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Document vectorization failed. Document ID: {DocumentId}, Task ID: {TaskId}", workerTask.DocumentId, workerTask.Id);
//            await SetFaildStateAsync(workerTask, ex.Message);
//            throw;
//        }

//        await SetComplateStateAsync(workerTask);
//    }

//    /// <inheritdoc/>
//    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, EmbeddingDocumentTaskMesage message)
//    {
//        var documentTask = await _databaseContext.WikiDocumentTasks
//             .FirstOrDefaultAsync(x => x.DocumentId == message.DocumentId && x.Id == message.TaskId);

//        // 不需要处理
//        if (documentTask == null || documentTask.State > (int)FileEmbeddingState.Processing)
//        {
//            return;
//        }

//        documentTask.State = (int)FileEmbeddingState.Failed;
//        documentTask.Message = ex.Message;

//        _databaseContext.WikiDocumentTasks.Update(documentTask);
//        await _databaseContext.SaveChangesAsync();

//        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
//    }

//    /// <inheritdoc/>
//    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMesage? message, Exception? ex)
//    {
//        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
//        return Task.FromResult(ConsumerState.Ack);
//    }

//    private async Task<(EmbeddingConfig? WikiConfig, AiEndpoint? AiEndpoint)> GetWikiConfigAsync(int wikiId)
//    {
//        var result = await _databaseContext.Wikis
//        .Where(x => x.Id == wikiId)
//        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
//        {
//            WikiConfig = new EmbeddingConfig
//            {
//                EmbeddingDimensions = a.EmbeddingDimensions,
//                EmbeddingBatchSize = a.EmbeddingBatchSize,
//                MaxRetries = a.MaxRetries,
//                EmbeddingModelTokenizer = a.EmbeddingModelTokenizer.JsonToObject<EmbeddingTokenizer>(),
//                EmbeddingModelId = a.EmbeddingModelId,
//            },
//            AiEndpoint = new AiEndpoint
//            {
//                Name = x.Name,
//                DeploymentName = x.DeploymentName,
//                Title = x.Title,
//                AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
//                Provider = x.AiProvider.JsonToObject<AiProvider>(),
//                ContextWindowTokens = x.ContextWindowTokens,
//                Endpoint = x.Endpoint,
//                Abilities = new ModelAbilities
//                {
//                    Files = x.Files,
//                    FunctionCall = x.FunctionCall,
//                    ImageOutput = x.ImageOutput,
//                    Vision = x.IsVision,
//                },
//                MaxDimension = x.MaxDimension,
//                TextOutput = x.TextOutput,
//                Key = x.Key,
//            }
//        }).FirstOrDefaultAsync();

//        return (result?.WikiConfig, result?.AiEndpoint);
//    }

//    private async Task SetExceptionStateAsync(Guid taskId, WorkerState state, Exception? ex = null)
//    {
//        _logger.LogError(ex, "Task processing failed.");
//        await SetStateAsync(taskId, state, ex?.Message);
//    }

//    private async Task SetStateAsync(Guid taskId, WorkerState state, string? message = null)
//    {
//        // 设置之前先检查状态
//        var workerTask = await _databaseContext.WorkerTasks.AsNoTracking()
//             .FirstOrDefaultAsync(x => x.Id == taskId);

//        // 不需要处理或有其它线程在执行
//        if (workerTask == null || workerTask.State > (int)WorkerState.Processing)
//        {
//            throw new BusinessException("当前任务已结束");
//        }

//        workerTask.State = (int)state;
//        if (!string.IsNullOrEmpty(message))
//        {
//            workerTask.Message = message;
//        }
//        else
//        {
//            workerTask.Message = state.ToJsonString();
//        }
//        _databaseContext.WorkerTasks.Update(workerTask);
//        await _databaseContext.SaveChangesAsync();
//    }
//}
