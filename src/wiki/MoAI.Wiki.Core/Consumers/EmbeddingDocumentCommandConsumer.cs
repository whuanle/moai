using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using MoAI.AiModel.Models;
using MoAI.AiModel.Services;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Extensions;
using MoAI.Storage.Queries;
using MoAI.Wiki.Models;
using MoAIDocument.Core.Consumers.Events;

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
    private readonly ILogger<EmbeddingDocumentCommandConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandConsumer"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="mediator"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    public EmbeddingDocumentCommandConsumer(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator, IServiceProvider serviceProvider, ILogger<EmbeddingDocumentCommandConsumer> logger)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _mediator = mediator;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMesage message)
    {
        var documentTask = await _databaseContext.WikiDocumentTasks
             .FirstOrDefaultAsync(x => x.DocumentId == message.DocumentId && x.Id == message.TaskId);

        // 不需要处理
        if (documentTask == null || documentTask.State > (int)FileEmbeddingState.Processing)
        {
            return;
        }

        await SetStartStateAsync(documentTask);

        var documenEntity = await _databaseContext.WikiDocuments.Where(x => x.Id == message.DocumentId).FirstOrDefaultAsync();

        if (documenEntity == null)
        {
            await SetFaildStateAsync(documentTask, "文档不存在");
            return;
        }

        var filePath = await _mediator.Send(new QueryFileLocalPathCommand
        {
            Visibility = FileVisibility.Private,
            ObjectKey = documenEntity.ObjectKey,
        });

        var (wikiConfig, aiEndpoint) = await GetWikiConfigAsync(documenEntity.WikiId);

        if (wikiConfig == null || aiEndpoint == null)
        {
            await SetFaildStateAsync(documentTask, "知识库未配置向量化模型");
            return;
        }

        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder().WithSimpleFileStorage(Path.GetTempPath());

        var textEmbeddingGeneration = _serviceProvider.GetKeyedService<ITextEmbeddingGeneration>(aiEndpoint.Provider);

        if (textEmbeddingGeneration == null)
        {
            textEmbeddingGeneration = _serviceProvider.GetKeyedService<ITextEmbeddingGeneration>(AiProvider.Custom);
        }

        if (textEmbeddingGeneration == null)
        {
            await SetFaildStateAsync(documentTask, "不支持的模型供应商");
            return;
        }

        var memoryDb = _serviceProvider.GetKeyedService<IMemoryDbClient>(_systemOptions.Wiki.DBType);

        if (memoryDb == null)
        {
            await SetFaildStateAsync(documentTask, "不支持的数据库");
            return;
        }

        textEmbeddingGeneration.Configure(memoryBuilder, aiEndpoint, wikiConfig);
        memoryDb.Configure(memoryBuilder, _systemOptions.Wiki.ConnectionString);

        var memoryClient = memoryBuilder.WithoutTextGenerator()
            .WithCustomTextPartitioningOptions(
            new TextPartitioningOptions
            {
                MaxTokensPerParagraph = documentTask.MaxTokensPerParagraph,
                OverlappingTokens = documentTask.OverlappingTokens,
            })
            .Build();

        // 先删除历史记录
        await memoryClient.DeleteDocumentAsync(documentTask.DocumentId.ToString(), index: documentTask.WikiId.ToString());

        var docs = new Microsoft.KernelMemory.Document()
        {
            Id = documentTask.DocumentId.ToString(),
        };

        // 自行读取流以便自定义文件名称
        using var documentStream = new FileStream(filePath.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        docs.Files.AddStream(documenEntity.FileName, documentStream);
        docs.AddTag("wikiId", documenEntity.WikiId.ToString());
        docs.AddTag("fileId", documenEntity.FileId.ToString());

        try
        {
            var taskId = await memoryClient.ImportDocumentAsync(docs, index: documentTask.WikiId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document vectorization failed. Document ID: {DocumentId}, Task ID: {TaskId}", documentTask.DocumentId, documentTask.Id);
            await SetFaildStateAsync(documentTask, ex.Message);
            throw;
        }

        await SetComplateStateAsync(documentTask);
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, EmbeddingDocumentTaskMesage message)
    {
        var documentTask = await _databaseContext.WikiDocumentTasks
             .FirstOrDefaultAsync(x => x.DocumentId == message.DocumentId && x.Id == message.TaskId);

        // 不需要处理
        if (documentTask == null || documentTask.State > (int)FileEmbeddingState.Processing)
        {
            return;
        }

        documentTask.State = (int)FileEmbeddingState.Failed;
        documentTask.Message = ex.Message;

        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();

        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
    }

    /// <inheritdoc/>
    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, EmbeddingDocumentTaskMesage? message, Exception? ex)
    {
        _logger.LogError(ex, message: "Document processing failed.{@Message}", message);
        return Task.FromResult(ConsumerState.Ack);
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
                EmbeddingBatchSize = a.EmbeddingBatchSize,
                MaxRetries = a.MaxRetries,
                EmbeddingModelTokenizer = a.EmbeddingModelTokenizer.JsonToObject<EmbeddingTokenizer>(),
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

    private async Task SetFaildStateAsync(MoAI.Database.Entities.WikiDocumentTaskEntity documentTask, string message)
    {
        documentTask.State = (int)FileEmbeddingState.Failed;
        documentTask.Message = message;
        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();
    }

    private async Task SetStartStateAsync(MoAI.Database.Entities.WikiDocumentTaskEntity documentTask)
    {
        documentTask.State = (int)FileEmbeddingState.Processing;
        documentTask.Message = "任务开始处理";
        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();
    }

    private async Task SetComplateStateAsync(MoAI.Database.Entities.WikiDocumentTaskEntity documentTask)
    {
        documentTask.State = (int)FileEmbeddingState.Successful;
        documentTask.Message = "任务处理完成";
        _databaseContext.WikiDocumentTasks.Update(documentTask);
        await _databaseContext.SaveChangesAsync();
    }
}
