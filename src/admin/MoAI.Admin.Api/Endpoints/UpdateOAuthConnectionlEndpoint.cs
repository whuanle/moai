// <copyright file="UpdateOAuthConnectionlEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Admin;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Queries;

namespace MoAI.Login.Endpoints;

/// <summary>
/// <inheritdoc cref="UpdateOAuthConnectionCommand"/>.
/// </summary>
[HttpPut($"{ApiPrefix.OAuth}/update")]
public class UpdateOAuthConnectionlEndpoint : Endpoint<UpdateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOAuthConnectionlEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public UpdateOAuthConnectionlEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateOAuthConnectionCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
