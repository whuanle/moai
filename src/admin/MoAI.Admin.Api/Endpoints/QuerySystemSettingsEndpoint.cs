// <copyright file="UpdateOAuthConnectionlEndpoint.cs" company="MoAI">
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
/// 查询系统配置.
/// </summary>
[HttpGet($"{ApiPrefix.Settings}")]
public class QuerySystemSettingsEndpoint : EndpointWithoutRequest<QuerySystemSettingsCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemSettingsEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public QuerySystemSettingsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public override async Task<QuerySystemSettingsCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        return await _mediator.Send(new QuerySystemSettingsCommand(), ct);
    }
}
