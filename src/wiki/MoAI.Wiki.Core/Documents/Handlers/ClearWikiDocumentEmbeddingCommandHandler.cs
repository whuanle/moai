using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using System.Transactions;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// <inheritdoc cref="ClearWikiDocumentEmbeddingCommand"/>
/// </summary>
public class ClearWikiDocumentEmbeddingCommandHandler : IRequestHandler<ClearWikiDocumentEmbeddingCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearWikiDocumentEmbeddingCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    /// <param name="serviceProvider"></param>
    public ClearWikiDocumentEmbeddingCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions, IServiceProvider? serviceProvider = null)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ClearWikiDocumentEmbeddingCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        var memoryDb = _serviceProvider.GetKeyedService<IMemoryDbClient>(_systemOptions.Wiki.DBType);
        if (memoryDb == null)
        {
            throw new BusinessException("不支持的文档数据库");
        }

        IKernelMemoryBuilder memoryBuilder = new KernelMemoryBuilder();
        memoryBuilder = memoryDb.Configure(memoryBuilder, _systemOptions.Wiki.ConnectionString);

        var memoryClient = memoryBuilder.WithSimpleFileStorage(Path.GetTempPath())
            .WithoutEmbeddingGenerator()
            .WithoutTextGenerator()
            .Build();

        if (request.DocumentId != null && request.DocumentId > 0)
        {
            var document = await _databaseContext.WikiDocuments
                .Where(x => x.WikiId == request.WikiId && x.Id == request.DocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (document == null)
            {
                throw new BusinessException("文档不存在") { StatusCode = 404 };
            }

            using (TransactionScope t = new TransactionScope(scopeOption: TransactionScopeOption.Suppress, asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled))
            {
                await memoryClient.DeleteDocumentAsync(documentId: document.Id.ToString(), index: document.WikiId.ToString());
            }

            await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocumentTasks.Where(x => x.DocumentId == request.DocumentId));
            await _databaseContext.SaveChangesAsync();
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

            await _databaseContext.SoftDeleteAsync(_databaseContext.WikiDocumentTasks.Where(x => x.WikiId == request.WikiId));

            await _databaseContext.Wikis.Where(x => x.Id == wiki.Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, false));
            await _databaseContext.SaveChangesAsync();
        }

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }
}
