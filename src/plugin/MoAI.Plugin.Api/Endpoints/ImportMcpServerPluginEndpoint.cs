// <copyright file="ImportMcpServerPluginEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 导入 mcp 服务.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/import_mcp")]
public class ImportMcpServerPluginEndpoint : Endpoint<ImportMcpServerPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportMcpServerPluginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public ImportMcpServerPluginEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(ImportMcpServerPluginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
