// <copyright file="OAuthRedirectEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Login.Endpoints;

/// <summary>
/// 创建 OAuth2.0 连接配置.
/// </summary>
[HttpPost($"{ApiPrefix.OAuth}/create")]
public class CreateOAuthConnectionlEndpoint : Endpoint<CreateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOAuthConnectionlEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public CreateOAuthConnectionlEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(CreateOAuthConnectionCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
