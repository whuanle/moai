// <copyright file="PreUploadWikiDocumentCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Store.Enums;
using MoAI.Wiki.Documents.Commands;
using MoAI.Wiki.Documents.Commands.Responses;

namespace MoAI.Wiki.Documents.Handlers;

/// <summary>
/// 预上传知识库文件.
/// </summary>
public class PreUploadWikiDocumentCommandHandler : IRequestHandler<PreUploadWikiDocumentCommand, PreloadWikiDocumentResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public PreUploadWikiDocumentCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<PreloadWikiDocumentResponse> Handle(PreUploadWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!FileStoreHelper.DocumentFormats.Contains(Path.GetExtension(request.FileName).ToLower()))
        {
            throw new BusinessException("不支持该文件格式") { StatusCode = 400 };
        }

        var existWiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).AnyAsync(cancellationToken);

        if (!existWiki)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        // 同一个知识库下不能有同名文件.
        var existDocument = await _databaseContext.WikiDocuments.Where(x => x.FileName == request.FileName).AnyAsync();

        if (existDocument)
        {
            throw new BusinessException("同一个知识库下不能有同名文件") { StatusCode = 409 };
        }

        var objectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName, prefix: $"wiki/{request.WikiId}");

        var result = await _mediator.Send(new PreUploadFileCommand
        {
            MD5 = request.MD5,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            Visibility = FileVisibility.Private,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2),
        });

        if (result.IsExist)
        {
            throw new BusinessException("同一个知识库下不能有同名文件，请联系管理员") { StatusCode = 409 };
        }

        return new PreloadWikiDocumentResponse
        {
            Visibility = FileVisibility.Private,
            FileId = result.FileId,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}
