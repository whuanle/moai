// <copyright file="SetSystemSettingsEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MoAI.Admin;
using MoAI.Admin.SystemSettings.Commands;
using MoAI.Admin.SystemSettings.Queries;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Queries;

namespace MoAI.Login.Endpoints;

/// <summary>
/// 更新系统设置.
/// </summary>
[HttpPut($"{ApiPrefix.Settings}")]
public class SetSystemSettingsEndpoint : Endpoint<SetSystemSettingsCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSystemSettingsEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public SetSystemSettingsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(SetSystemSettingsCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}