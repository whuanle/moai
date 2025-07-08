// <copyright file="ComplateUploadWikiDocumentCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 完成上传文档.
/// </summary>
public class ComplateUploadWikiDocumentCommandHandler : IRequestHandler<ComplateUploadWikiDocumentCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplateUploadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public ComplateUploadWikiDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ComplateUploadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        _ = await _mediator.Send(new ComplateFileUploadCommand
        {
            FileId = request.FileId,
            IsSuccess = request.IsSuccess,
        });

        if (!request.IsSuccess)
        {
            // 上传失败，删除数据库记录
            var document = await _databaseContext.WikiDocuments
                .Where(x => x.Id == request.DocumentId && x.WikiId == request.WikiId)
                .FirstOrDefaultAsync(cancellationToken);

            if (document != null)
            {
                _databaseContext.WikiDocuments.Remove(document);
                await _databaseContext.SaveChangesAsync(cancellationToken);
            }

            return EmptyCommandResponse.Default;
        }

        return EmptyCommandResponse.Default;
    }
}
