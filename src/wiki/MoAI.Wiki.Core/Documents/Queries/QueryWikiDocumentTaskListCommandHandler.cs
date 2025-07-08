// <copyright file="QueryWikiDocumentTaskListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Document.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.User.Queries;
using MoAI.Wiki.Documents.Queries.Responses;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询文档任务列表.
/// </summary>
public class QueryWikiDocumentTaskListCommandHandler : IRequestHandler<QueryWikiDocumentTaskListCommand, IReadOnlyCollection<WikiDocumentTaskItem>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentTaskListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiDocumentTaskListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<WikiDocumentTaskItem>> Handle(QueryWikiDocumentTaskListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId && x.Id == request.DocumentId);
        var fileEntity = await query.Join(_databaseContext.Files, a => a.FileId, b => b.Id, (a, b) => new QueryWikiDocumentListItem
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

        if (fileEntity == null)
        {
            throw new BusinessException("未找到文档") { StatusCode = 404 };
        }

        var result = await _databaseContext.WikiDocuments
            .Where(x => x.Id == request.DocumentId)
            .Join(_databaseContext.WikiDocumentTasks, a => a.Id, b => b.DocumentId, (a, b) => new WikiDocumentTaskItem
            {
                DocumentId = a.Id,
                CreateTime = b.CreateTime,
                CreateUserId = b.CreateUserId,
                UpdateTime = b.UpdateTime,
                UpdateUserId = b.UpdateUserId,
                FileId = a.FileId,
                FileName = fileEntity.FileName,
                FileSize = fileEntity.FileSize,
                ContentType = fileEntity.ContentType,
                WikiId = a.WikiId,
                TaskTag = b.TaskTag,
                State = (FileEmbeddingState)b.State,
                Message = b.Message,
                CreateUserName = string.Empty,
                UpdateUserName = string.Empty,
                Id = b.Id,
                MaxTokensPerParagraph = b.MaxTokensPerParagraph,
                OverlappingTokens = b.OverlappingTokens,
                Tokenizer = b.Tokenizer,
            }).OrderByDescending(x => x.CreateTime).ToListAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand { Items = result });

        return result;
    }
}