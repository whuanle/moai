// <copyright file="ClearWikiDocumentEmbeddingCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearWikiDocumentEmbeddingCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    public ClearWikiDocumentEmbeddingCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ClearWikiDocumentEmbeddingCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        var memoryClient = new KernelMemoryBuilder()
            .WithSimpleFileStorage(Path.GetTempPath())
            .WithoutEmbeddingGenerator()
            .WithoutTextGenerator()
        .WithPostgresMemoryDb(new PostgresConfig
        {
            ConnectionString = _systemOptions.Wiki.Database,
        })
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
