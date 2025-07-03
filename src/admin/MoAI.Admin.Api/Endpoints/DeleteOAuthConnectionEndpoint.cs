// <copyright file="DeleteOAuthConnectionEndpoint.cs" company="MoAI">
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
/// 删除认证方式.
/// </summary>
[HttpDelete($"{ApiPrefix.OAuth}/delete")]
public class DeleteOAuthConnectionEndpoint : Endpoint<DeleteOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteOAuthConnectionEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public DeleteOAuthConnectionEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteOAuthConnectionCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
