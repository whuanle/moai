// <copyright file="QueryWikiWebDocumentListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.User.Queries;
using MoAI.Wiki.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.WebDocuments.Queries;

/// <summary>
/// 查询知识库文档列表.
/// </summary>
public class QueryWikiWebDocumentListCommandHandler : IRequestHandler<QueryWikiWebDocumentListCommand, QueryWikiDocumentListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiWebDocumentListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiWebDocumentListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentListCommandResponse> Handle(QueryWikiWebDocumentListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext
            .WikiDocuments.Where(x => _databaseContext.WikiWebDocuments.Where(a => a.WikiWebConfigId == request.WikiWebConfigId)
            .Any(a => a.WikiDocumentId == x.Id));

        if (!string.IsNullOrEmpty(request.Query))
        {
            query = query.Where(x => x.FileName.Contains(request.Query));
        }

        var totalCount = await query.CountAsync();

        var result = await query.Join(_databaseContext.Files.Where(x => x.IsUploaded), a => a.FileId, b => b.Id, (a, b) => new QueryWikiDocumentListItem
        {
            DocumentId = a.Id,
            FileName = b.FileName,
            FileSize = b.FileSize,
            ContentType = b.ContentType,
            CreateTime = a.CreateTime,
            CreateUserId = a.CreateUserId,
            UpdateTime = a.UpdateTime,
            UpdateUserId = a.UpdateUserId,
            Embedding = _databaseContext.WikiDocumentTasks.Any(x => x.DocumentId == a.Id && x.State == (int)FileEmbeddingState.Successful)
        }).OrderBy(x => x.FileName).Take(request.Take).Skip(request.Skip).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = result
        });

        return new QueryWikiDocumentListCommandResponse
        {
            Total = totalCount,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
            Items = result
        };
    }
}
