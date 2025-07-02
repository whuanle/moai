// <copyright file="RefreshTokenEndpoint.cs" company="MoAI">
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
/// 刷新token.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/refresh_token")]
[AllowAnonymous]
public class RefreshTokenEndpoint : Endpoint<RefreshTokenCommand, RefreshTokenCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public RefreshTokenEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>、
    public override async Task<RefreshTokenCommandResponse> ExecuteAsync(RefreshTokenCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}