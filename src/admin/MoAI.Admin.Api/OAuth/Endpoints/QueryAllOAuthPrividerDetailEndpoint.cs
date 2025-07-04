﻿// <copyright file="QueryAllOAuthPrividerDetailEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin;
using MoAI.Admin.OAuth.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Queries;
using MoAI.Public.Queries;

namespace MoAI.Admin.OAuth.Endpoints;

/// <summary>
/// 查询所有认证方式.
/// </summary>
[HttpGet($"{ApiPrefix.OAuth}/detail_list")]
public class QueryAllOAuthPrividerDetailEndpoint : EndpointWithoutRequest<QueryAllOAuthPrividerDetailCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthPrividerDetailEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryAllOAuthPrividerDetailEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAllOAuthPrividerDetailCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(new QueryAllOAuthPrividerDetailCommand(), ct);
    }
}
