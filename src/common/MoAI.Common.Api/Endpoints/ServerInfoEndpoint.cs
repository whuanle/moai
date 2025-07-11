// <copyright file="ServerInfoEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Common.Queries;
using MoAI.Common.Queries.Response;

namespace MoAI.Common.Endpoints;

/// <summary>
/// 获取服务器信息.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/serverinfo")]
[AllowAnonymous]
public class ServerInfoEndpoint : EndpointWithoutRequest<QueryServerInfoCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerInfoEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ServerInfoEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override Task<QueryServerInfoCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return _mediator.Send(new QueryServerInfoCommand { }, ct);
    }
}
