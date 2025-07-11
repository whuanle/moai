﻿// <copyright file="QueryWikiDocumentInfoEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Documents.Queries;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Endpoints;

/// <summary>
/// 查询文档信息.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/document/document_info")]
public class QueryWikiDocumentInfoEndpoint : Endpoint<QueryWikiDocumentInfoCommand, QueryWikiDocumentListItem>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentInfoEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryWikiDocumentInfoEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryWikiDocumentListItem> ExecuteAsync(QueryWikiDocumentInfoCommand req, CancellationToken ct)
    {
        var userIsWikiUser = await _mediator.Send(new QueryUserIsWikiUserCommand
        {
            UserId = _userContext.UserId,
            WikiId = req.WikiId
        });

        if (!userIsWikiUser.IsWikiUser)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req);
    }
}
