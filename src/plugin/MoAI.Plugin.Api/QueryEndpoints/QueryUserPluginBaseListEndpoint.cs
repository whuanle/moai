// <copyright file="QueryUserPluginBaseListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.QueryEndpoints;

/// <summary>
/// 查询用户插件简要信息列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/user_plugin_list")]
public class QueryUserPluginBaseListEndpoint : Endpoint<QueryUserPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserPluginBaseListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserPluginBaseListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginBaseListCommandResponse> ExecuteAsync(QueryUserPluginBaseListCommand req, CancellationToken ct)
    {
        return await _mediator.Send(
            new QueryUserPluginBaseListCommand
            {
                IsOwn = req.IsOwn,
                Name = req.Name,
                Type = req.Type
            },
            ct);
    }
}
