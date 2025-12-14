using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using MoAI.AI.MemoryDb;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Extesions;
using System.Transactions;

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

        // 清空向量不需要存储时，向量维度随便填
        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(aiModel, 500);
        var memoryClient = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        if (request.DocumentId != null && request.DocumentId > 0)
        {
            var document = await _databaseContext.WikiDocuments
                .Where(x => x.WikiId == request.WikiId && x.Id == request.DocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (document == null)
            {
                throw new BusinessException("文档不存在") { StatusCode = 404 };
            }

            var wikiIndex = request.WikiId.ToString();
            var documentIndex = request.DocumentId.ToString()!;

            // 删除这个文档以前的向量
            // __document_id
            var records = memoryClient.GetListAsync(
                index: wikiIndex,
                limit: -1,
                filters: [MemoryFilters.ByTag("document_id", documentIndex)],
                cancellationToken: CancellationToken.None);

            await foreach (var record in records.WithCancellation(CancellationToken.None).ConfigureAwait(false))
            {
                await memoryClient.DeleteAsync(index: wikiIndex, record, cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
        }
        else
        {
            var wiki = await _databaseContext.Wikis
                .Where(x => x.Id == request.WikiId)
                .FirstOrDefaultAsync(cancellationToken);

            if (wiki == null)
            {
                throw new BusinessException("知识库不存在") { StatusCode = 404 };
            }

            using (TransactionScope t = new TransactionScope(scopeOption: TransactionScopeOption.Suppress, asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled))
            {
                await memoryClient.DeleteIndexAsync(index: wiki.Id.ToString());
            }

            await _databaseContext.Wikis.Where(x => x.Id == wiki.Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, false));
            await _databaseContext.SaveChangesAsync();
        }

        return EmptyCommandResponse.Default;
    }
}
