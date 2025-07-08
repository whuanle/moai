// <copyright file="DeleteWikiDocumentCommandHandler.cs" company="MoAI">
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
using MoAI.Storage.Commands;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 删除知识库文档.
/// </summary>
public class DeleteWikiDocumentCommandHandler : IRequestHandler<DeleteWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    public DeleteWikiDocumentCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        // 删除数据库记录，附属表不需要删除
        var document = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == request.WikiId && x.Id == request.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            throw new BusinessException("文档不存在") { StatusCode = 404 };
        }

        _databaseContext.WikiDocuments.Remove(document);
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

        await memoryClient.DeleteDocumentAsync(documentId: document.Id.ToString(), index: document.WikiId.ToString());

        // 删除 oss 文件
        await _mediator.Send(new DeleteFileCommand { FileId = document.FileId });

        var documentCount = await _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId).CountAsync();
        if (documentCount == 0)
        {
            await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).ExecuteUpdateAsync(x => x.SetProperty(a => a.IsLock, false));
        }

        return EmptyCommandResponse.Default;
    }
}
