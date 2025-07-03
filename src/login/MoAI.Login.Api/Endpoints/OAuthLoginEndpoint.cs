// <copyright file="LoginEndpoint.cs" company="MoAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Login.Commands;

namespace MoAI.Login.Endpoints;

/// <summary>
/// OAuth登录.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/oauth_login")]
[AllowAnonymous]
public class OAuthLoginEndpoint : Endpoint<OAuthLoginCommand, OAuthLoginCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthLoginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public OAuthLoginEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<OAuthLoginCommandResponse> ExecuteAsync(OAuthLoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
