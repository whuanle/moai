// <copyright file="QueryPluginBaseListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 插件列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/plugin_list")]
public class QueryPluginBaseListEndpoint : Endpoint<QueryPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginBaseListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPluginBaseListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginBaseListCommandResponse> ExecuteAsync(QueryPluginBaseListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(
            new QueryPluginBaseListCommand
            {
                UserId = _userContext.UserId,
                IsOwn = req.IsOwn,
                Name = req.Name,
                Type = req.Type
            },
            ct);
    }
}