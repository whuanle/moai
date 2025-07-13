// <copyright file="QueryWikiListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 获取用户有权访问的知识库列表.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/query_wiki_list")]
public class QueryWikiListEndpoint : EndpointWithoutRequest<IReadOnlyCollection<QueryWikiSimpleInfoResponse>>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyCollection<QueryWikiSimpleInfoResponse>> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(new QueryWikiListCommand { UserId = _userContext.UserId });
    }
}
