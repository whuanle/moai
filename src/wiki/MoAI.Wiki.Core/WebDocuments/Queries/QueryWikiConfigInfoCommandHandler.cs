// <copyright file="QueryWikiConfigInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.User.Queries;
using MoAI.Wiki.WebDocuments.Queries.Responses;

namespace MoAI.Wiki.WebDocuments.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiConfigInfoCommand"/>
/// </summary>
public class QueryWikiConfigInfoCommandHandler : IRequestHandler<QueryWikiConfigInfoCommand, QueryWikiConfigInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiConfigInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiConfigInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiConfigInfoCommandResponse> Handle(QueryWikiConfigInfoCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.WikiWebConfigs
            .Where(x => x.Id == request.WikiWebConfigId && x.WikiId == request.WikiId)
            .Select(x => new QueryWikiConfigInfoCommandResponse
            {
                WikiConfigId = x.Id,
                Title = x.Title,
                Address = x.Address,
                IsCrawlOther = x.IsCrawlOther,
                IsAutoEmbedding = x.IsAutoEmbedding,
                IsWaitJs = x.IsWaitJs,
                LimitAddress = x.LimitAddress,
                LimitMaxCount = x.LimitMaxCount,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            throw new BusinessException("未找到知识库爬虫配置");
        }

        await _mediator.Send(new FillUserInfoCommand { Items = new[] { result } });

        return result;
    }
}
