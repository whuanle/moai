using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.ChatCompletion;
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
    public ClearWikiDocumentEmbeddingCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions, IServiceProvider serviceProvider, IAiClientBuilder aiClientBuilder)
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
        var wikiEntity = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).FirstOrDefaultAsync();

        if (wikiEntity == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 500 };
        }

        var aiModel = await _databaseContext.QueryWikiAiModelAsync(request.WikiId);

        if (aiModel == null)
        {
            return EmptyCommandResponse.Default;
        }

        var documentIdsQuery = _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && x.IsEmbedding);

        if (request.ClearAllDocuments == true)
        {
        }
        else if (request.DocumentIds != null && request.DocumentIds.Count > 0)
        {
            var documentIds = request.DocumentIds.ToArray();
            documentIdsQuery = documentIdsQuery.Where(x => documentIds.Contains(x.Id));
        }
        else
        {
            throw new BusinessException("未正确配置要清空的文档ID");
        }

        var offsetId = 0;
        const int limit = 100;

        // 清空向量不需要存储时，向量维度随便填
        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiModel, 500);
        var memoryClient = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        // 分批 清空向量
        while (true)
        {
            var documentIds = await documentIdsQuery
                .Where(x => x.Id > offsetId)
                .OrderBy(x => x.Id)
                .Take(limit)
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);

            if (documentIds.Length == 0)
            {
                break;
            }

            offsetId = documentIds.Max();

            var embeddingIds = await _databaseContext.WikiDocumentChunkEmbeddings
                .Where(x => documentIds.Contains(x.DocumentId))
                .Select(x => x.Id)
                .ToArrayAsync();

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

            await _databaseContext.WhereUpdateAsync(
                _databaseContext.WikiDocuments.Where(x => documentIds.Contains(x.Id)),
                x => x.SetProperty(a => a.IsEmbedding, false));
            await _databaseContext.SaveChangesAsync();
        }

        var documentEmbeddingCount = await _databaseContext.WikiDocumentChunkEmbeddings.Where(x => x.WikiId == request.WikiId).CountAsync();

        if (request.IsAutoDeleteIndex && documentEmbeddingCount == 0)
        {
            await memoryClient.DeleteIndexAsync(index: request.WikiId.ToString());
            await _databaseContext.WhereUpdateAsync(_databaseContext.Wikis.Where(x => x.Id == request.WikiId), x => x.SetProperty(a => a.IsLock, false));
            await _databaseContext.SaveChangesAsync();
        }

        return EmptyCommandResponse.Default;
    }
}
