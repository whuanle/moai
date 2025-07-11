// <copyright file="QueryPluginListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;
using MoAI.Public.Queries;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 获取插件的概要信息.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/plugin_list")]
public class QueryPluginInfoListEndpoint : Endpoint<QueryPluginInfoListCommand, QueryPluginInfoListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginInfoListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPluginInfoListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginInfoListCommandResponse> ExecuteAsync(QueryPluginInfoListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}