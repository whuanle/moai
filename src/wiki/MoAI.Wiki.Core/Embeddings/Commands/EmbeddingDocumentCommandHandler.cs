using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Embedding.Models;
using MoAI.Wiki.Models;
using MoAIDocument.Core.Consumers.Events;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 向量化文档.
/// </summary>
public class EmbeddingDocumentCommandHandler : IRequestHandler<EmbeddingDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="messagePublisher"></param>
    public EmbeddingDocumentCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions, IMessagePublisher messagePublisher)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(EmbeddingDocumentCommand request, CancellationToken cancellationToken)
    {
        var documentTask = await _databaseContext.WorkerTasks
            .Where(x => x.BindType == "embedding" && x.BindId == request.DocumentId && x.State <= (int)WorkerState.Processing)
            .AnyAsync();

        if (documentTask == true)
        {
            throw new BusinessException("当前文档已在处理任务，请勿重复添加") { StatusCode = 409 };
        }

        // 检查切片
        var existChunks = await _databaseContext.WikiDocumentChunkContentPreviews.Where(x => x.DocumentId == request.DocumentId).AnyAsync();
        var existMetadatas = await _databaseContext.WikiDocumentChunkMetadataPreviews.Where(x => x.DocumentId == request.DocumentId).AnyAsync();

        if (!existChunks && !existMetadatas)
        {
            throw new BusinessException("该文档无可向量化的内容");
        }

        if (!request.IsEmbedSourceText && !existMetadatas)
        {
            throw new BusinessException("该文档无可向量化的元数据内容");
        }

        var fileId = await _databaseContext.WikiDocuments.Where(x => x.Id == request.DocumentId)
            .Select(x => x.FileId)
            .FirstOrDefaultAsync();

        if (fileId == default)
        {
            throw new BusinessException("知识库文档不存在") { StatusCode = 404 };
        }

        var teamWikiConfig = await _databaseContext.Wikis
            .Where(x => x.Id == request.WikiId)
            .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, b) => new
            {
                b.Id,
                b.AiModelType,
            }).FirstOrDefaultAsync();

        if (teamWikiConfig == null || !AiModelType.Embedding.ToString().Equals(teamWikiConfig.AiModelType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("知识库未配置向量化模型") { StatusCode = 409 };
        }

        var documentTaskEntity = new WorkerTaskEntity
        {
            State = (int)WorkerState.Wait,
            BindType = "embedding",
            BindId = request.DocumentId,
            Data = new WikiDocumentEmbeddingConfig
            {
                WikiId = request.WikiId,
                DocumentId = request.DocumentId,
                IsEmbedSourceText = request.IsEmbedSourceText,
                ThreadCount = request.ThreadCount ?? 5
            }.ToJsonString(),
            Message = "任务已创建"
        };

        await _databaseContext.WorkerTasks.AddAsync(documentTaskEntity, cancellationToken);
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.Wikis.Where(x => x.Id == request.WikiId),
            x => x.SetProperty(x => x.IsLock, true));
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 后台处理
        var embeddingDocumentEvent = new EmbeddingDocumentTaskMesage
        {
            DocumentId = request.DocumentId,
            WikiId = request.WikiId,
            TaskId = documentTaskEntity.Id,
        };

        await _messagePublisher.AutoPublishAsync(embeddingDocumentEvent);

        return EmptyCommandResponse.Default;
    }
}
