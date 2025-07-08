// <copyright file="QueryWikiDocumentInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.User.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询知识库文档文件.
/// </summary>
public class QueryWikiDocumentInfoCommandHandler : IRequestHandler<QueryWikiDocumentInfoCommand, QueryWikiDocumentListItem>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiDocumentInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentListItem> Handle(QueryWikiDocumentInfoCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.WikiDocuments.Where(x => x.Id == request.DocumentId);

        var result = await query.Join(_databaseContext.Files, a => a.FileId, b => b.Id, (a, b) => new QueryWikiDocumentListItem
        {
            DocumentId = a.Id,
            FileName = b.FileName,
            FileSize = b.FileSize,
            ContentType = b.ContentType,
            CreateTime = a.CreateTime,
            CreateUserId = a.CreateUserId,
            UpdateTime = a.UpdateTime,
            UpdateUserId = a.UpdateUserId
        }).FirstOrDefaultAsync();

        if (result == null)
        {
            throw new BusinessException("文档不存在") { StatusCode = 404 };
        }

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = result
        });

        return result;
    }
}
