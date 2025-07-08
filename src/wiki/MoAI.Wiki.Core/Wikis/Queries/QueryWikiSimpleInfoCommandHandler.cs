// <copyright file="QueryWikiSimpleInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库简单信息.
/// </summary>
public class QueryWikiSimpleInfoCommandHandler : IRequestHandler<QueryWikiSimpleInfoCommand, QueryWikiSimpleInfoResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiSimpleInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiSimpleInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiSimpleInfoResponse> Handle(QueryWikiSimpleInfoCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId)
            .Select(x => new QueryWikiSimpleInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
            }).FirstOrDefaultAsync();

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        return wiki;
    }
}
