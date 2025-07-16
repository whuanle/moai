// <copyright file="QueryWikiBaseListEndpoint.cs" company="MoAI">
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
[HttpPost($"{ApiPrefix.Prefix}/query_wiki_list")]
public class QueryWikiBaseListEndpoint : Endpoint<QueryWikiBaseListCommand, IReadOnlyCollection<QueryWikiInfoResponse>>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiBaseListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiBaseListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyCollection<QueryWikiInfoResponse>> ExecuteAsync(QueryWikiBaseListCommand req, CancellationToken ct)
    {
        var newReq = new QueryWikiBaseListCommand
        {
            UserId = _userContext.UserId,
            IsOwn = req.IsOwn,
            IsPublic = req.IsPublic,
            IsUser = req.IsUser
        };

        return await _mediator.Send(newReq, ct);
    }
}
