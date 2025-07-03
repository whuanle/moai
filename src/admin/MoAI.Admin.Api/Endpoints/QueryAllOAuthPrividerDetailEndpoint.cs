// <copyright file="QueryAllOAuthPrividerDetailEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin;
using MoAI.Admin.OAuth.Queries.Responses;
using MoAI.Login.Queries;

namespace MoAI.Login.Endpoints;

/// <summary>
/// 查询所有认证方式.
/// </summary>
[HttpGet($"{ApiPrefix.OAuth}/detail_list")]
public class QueryAllOAuthPrividerDetailEndpoint : EndpointWithoutRequest<QueryAllOAuthPrividerDetailCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthPrividerDetailEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public QueryAllOAuthPrividerDetailEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<QueryAllOAuthPrividerDetailCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(new QueryAllOAuthPrividerDetailCommand(), ct);
    }
}
