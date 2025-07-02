// <copyright file="LoginEndpoint.cs" company="MoAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Endpoints;

/// <summary>
/// 使用 OAuth 绑定已存在的账号.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/oauth_bind_account")]
public class OAuthBindExistAccountEndpoint : Endpoint<OAuthBindExistAccountCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthBindExistAccountEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public OAuthBindExistAccountEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(OAuthBindExistAccountCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
