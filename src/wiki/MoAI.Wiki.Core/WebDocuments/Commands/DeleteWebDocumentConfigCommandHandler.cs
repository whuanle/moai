// <copyright file="DeleteWebDocumentConfigCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;
using System.Transactions;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// <inheritdoc cref="DeleteWebDocumentConfigCommand"/>
/// </summary>
public class DeleteWebDocumentConfigCommandHandler : IRequestHandler<DeleteWebDocumentConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWebDocumentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="mediator"></param>
    public DeleteWebDocumentConfigCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWebDocumentConfigCommand request, CancellationToken cancellationToken)
    {
        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        if (request.IsDeleteWebDocuments)
        {
            await NewMethod(request, cancellationToken);
        }

        // 删除配置
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebConfigs.Where(x => x.Id == request.WikiWebConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebDocuments.Where(x => x.WikiWebConfigId == request.WikiWebConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebCrawleTasks.Where(x => x.WikiWebConfigId == request.WikiWebConfigId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WikiWebCrawlePageStates.Where(x => x.WikiWebConfigId == request.WikiWebConfigId));

        transactionScope.Complete();

        return EmptyCommandResponse.Default;
    }

    private async Task NewMethod(DeleteWebDocumentConfigCommand request, CancellationToken cancellationToken)
    {
        var webDocumentIds = await _databaseContext.WikiWebDocuments.Where(x => x.WikiId == request.WikiId).Select(x => x.WikiDocumentId)
            .ToArrayAsync();

        var documents = await _databaseContext.WikiDocuments
            .Where(x => webDocumentIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);

        _databaseContext.WikiDocuments.RemoveRange(documents);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 删除文档向量
        var memoryClient = new KernelMemoryBuilder()
            .WithSimpleFileStorage(Path.GetTempPath())
            .WithoutEmbeddingGenerator()
            .WithoutTextGenerator()
            .WithPostgresMemoryDb(new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.Database,
            })
            .Build();

        foreach (var document in documents)
        {
            await memoryClient.DeleteDocumentAsync(documentId: document.Id.ToString(), index: document.WikiId.ToString());

            // 删除 oss 文件
            await _mediator.Send(new DeleteFileCommand { FileId = document.FileId });
        }
    }
}
