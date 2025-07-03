// <copyright file="RegisterUserEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Login.Endpoints;

/// <summary>
/// 注册账号.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/register")]
[AllowAnonymous]
public class RegisterUserEndpoint : Endpoint<RegisterUserCommand, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public RegisterUserEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override Task<SimpleInt> ExecuteAsync(RegisterUserCommand req, CancellationToken ct)
        => _mediator.Send(req, ct);
}
