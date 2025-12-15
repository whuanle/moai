using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.MemoryDb;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Extesions;

namespace MoAI.Wiki.Embeddings.Commands;

/// <summary>
/// <inheritdoc cref="ClearWikiDocumentEmbeddingCommand"/>
/// </summary>
public class ClearWikiDocumentEmbeddingCommandHandler : IRequestHandler<ClearWikiDocumentEmbeddingCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiClientBuilder _aiClientBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearWikiDocumentEmbeddingCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="aiClientBuilder"></param>
    public ClearWikiDocumentEmbeddingCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions, IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder = null)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
        _serviceProvider = serviceProvider;
        _aiClientBuilder = aiClientBuilder;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ClearWikiDocumentEmbeddingCommand request, CancellationToken cancellationToken)
    {
        var aiModel = await _databaseContext.QueryWikiAiModelAsync(request.WikiId);

        if (aiModel == null)
        {
            throw new BusinessException("知识库未配置嵌入模型") { StatusCode = 400 };
        }

        // 清空向量
        var embeddingIdsQuery = _databaseContext.WikiDocumentChunkEmbeddings
            .Where(x => x.WikiId == request.WikiId);

        if (request.DocumentId != null && request.DocumentId > 0)
        {
            embeddingIdsQuery = embeddingIdsQuery.Where(x => x.DocumentId == request.DocumentId);

            await _databaseContext.WikiDocuments.Where(x => x.Id == request.WikiId && x.Id == request.DocumentId)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.IsEmbedding, false));
            await _databaseContext.SaveChangesAsync();
        }

        var embeddingIds = await embeddingIdsQuery
        .Select(x => x.Id)
        .ToArrayAsync();

        // 清空向量不需要存储时，向量维度随便填
        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiModel, 500);
        var memoryClient = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        foreach (var item in embeddingIds)
        {
            await memoryClient.DeleteAsync(index: request.WikiId.ToString(), new Microsoft.KernelMemory.MemoryStorage.MemoryRecord
            {
                Id = item.ToString("N")
            });
        }

        await _databaseContext.WikiDocumentChunkEmbeddings
            .Where(x => embeddingIds.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);

        var documentEmbeddingCount = await _databaseContext.WikiDocumentChunkEmbeddings.Where(x => x.WikiId == request.WikiId).CountAsync();

        if (documentEmbeddingCount == 0)
        {
            await memoryClient.DeleteIndexAsync(index: request.WikiId.ToString());
            await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, false));
            await _databaseContext.SaveChangesAsync();
        }

        return EmptyCommandResponse.Default;
    }
}
