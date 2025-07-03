// <copyright file="LoginEndpoint.cs" company="MoAI">
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
/// 用户登录.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/login")]
[AllowAnonymous]
public class LoginEndpoint : Endpoint<LoginCommand, LoginCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public LoginEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<LoginCommandResponse> ExecuteAsync(LoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
