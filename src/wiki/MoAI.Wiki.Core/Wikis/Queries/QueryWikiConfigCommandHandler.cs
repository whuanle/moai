// <copyright file="QueryWikiConfigCommandHandler.cs" company="MoAI">
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

namespace MoAI.Wiki.Wikis.Queries;

public class QueryWikiConfigCommandHandler : IRequestHandler<QueryWikiConfigCommand, QueryWikiConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiConfigCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiConfigCommandResponse> Handle(QueryWikiConfigCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId)
            .Select(x => new QueryWikiConfigCommandResponse
            {
                EmbeddingDimensions = x.EmbeddingDimensions,
                EmbeddingModelId = x.EmbeddingModelId,
                EmbeddingModelTokenizer = x.EmbeddingModelTokenizer,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                IsLock = x.IsLock,
                EmbeddingBatchSize = x.EmbeddingBatchSize,
                MaxRetries = x.MaxRetries,
                WikiId = x.Id,
            }).FirstOrDefaultAsync();

        if (result == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 500 };
        }

        await _mediator.Send(new FillUserInfoCommand { Items = (IReadOnlyCollection<Infra.Models.AuditsInfo>)result });
        return result;
    }
}
