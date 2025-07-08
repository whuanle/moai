// <copyright file="CreateWikiEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Endpoints;

/// <summary>
/// 获取用户有权访问的知识库列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/query_wiki_list")]
public class QueryWikiListEndpoint : Endpoint<QueryWikiListCommand, IReadOnlyCollection<QueryWikiSimpleInfoResponse>>
{
    private readonly IMediator _mediator;
    public QueryWikiListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
    }
    public override async Task<IReadOnlyCollection<QueryWikiSimpleInfoResponse>> ExecuteAsync(QueryWikiListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req);
    }
}
