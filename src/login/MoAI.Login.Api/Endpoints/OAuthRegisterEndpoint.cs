// <copyright file="OAuthRegisterEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Login.Commands;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Endpoints;

/// <summary>
/// OAuth 注册.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/oauth_register")]
[AllowAnonymous]
public class OAuthRegisterEndpoint : Endpoint<OAuthRegisterCommand, LoginCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthRegisterEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public OAuthRegisterEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<LoginCommandResponse> ExecuteAsync(OAuthRegisterCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
