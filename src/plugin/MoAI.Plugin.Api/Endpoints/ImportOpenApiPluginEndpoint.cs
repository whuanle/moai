// <copyright file="ImportOpenApiPluginEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Common.Queries;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 完成 openapi 文件上传.
/// </summary>
[EndpointGroupName("store")]
[HttpPost($"{ApiPrefix.Prefix}/import_openapi")]
public class ImportOpenApiPluginEndpoint : Endpoint<ImportOpenApiPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiPluginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public ImportOpenApiPluginEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleInt> ExecuteAsync(ImportOpenApiPluginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}
