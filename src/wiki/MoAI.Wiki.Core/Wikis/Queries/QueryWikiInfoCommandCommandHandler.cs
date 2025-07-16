// <copyright file="QueryWikiInfoCommandCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库简单信息.
/// </summary>
public class QueryWikiInfoCommandCommandHandler : IRequestHandler<QueryWikiInfoCommand, QueryWikiInfoResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiInfoCommandCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiInfoCommandCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiInfoResponse> Handle(QueryWikiInfoCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId)
            .Select(x => new QueryWikiInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
                EmbeddingDimensions = x.EmbeddingDimensions,
                EmbeddingModelId = x.EmbeddingModelId,
                EmbeddingModelTokenizer = x.EmbeddingModelTokenizer.JsonToObject<EmbeddingTokenizer>(),
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                IsLock = x.IsLock,
                EmbeddingBatchSize = x.EmbeddingBatchSize,
                MaxRetries = x.MaxRetries,
            }).FirstOrDefaultAsync();

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        return wiki;
    }
}
