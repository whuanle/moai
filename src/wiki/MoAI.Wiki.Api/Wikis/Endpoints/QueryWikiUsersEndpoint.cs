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
/// 获取知识库协作者列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/query_wiki_users")]
public class QueryWikiUsersEndpoint : Endpoint<QueryWikiUsersCommand, QueryWikiUsersCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public QueryWikiUsersEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    public override async Task<QueryWikiUsersCommandResponse> ExecuteAsync(QueryWikiUsersCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiRoot)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}