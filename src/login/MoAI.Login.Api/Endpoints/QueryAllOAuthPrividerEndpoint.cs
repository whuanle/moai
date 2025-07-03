// <copyright file="QueryAllOAuthPrividerEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Endpoints;

/// <summary>
/// 获取第三方登录列表.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/oauth_prividers")]
[AllowAnonymous]
public class QueryAllOAuthPrividerEndpoint : EndpointWithoutRequest<QueryAllOAuthPrividerCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthPrividerEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public QueryAllOAuthPrividerEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override Task<QueryAllOAuthPrividerCommandResponse> ExecuteAsync(CancellationToken ct)
        => _mediator.Send(new QueryAllOAuthPrividerCommand { }, ct);
}
