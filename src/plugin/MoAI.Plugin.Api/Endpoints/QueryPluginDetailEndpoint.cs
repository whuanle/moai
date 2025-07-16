// <copyright file="QueryPluginInfoListEndpoint.cs" company="MoAI">
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

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 获取插件的详细信息.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/plugin_detail")]
public class QueryPluginDetailEndpoint : Endpoint<QueryPluginDetailCommand, QueryPluginDetailCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginDetailEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPluginDetailEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginDetailCommandResponse> ExecuteAsync(QueryPluginDetailCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryUserIsPluginCreatorCommand
        {
            PluginId = req.PluginId,
            UserId = _userContext.UserId
        });

        if (!isCreator.Value)
        {
            throw new BusinessException("无权操作该插件.");
        }

        return await _mediator.Send(
            new QueryPluginDetailCommand
            {
                PluginId = req.PluginId,
            },
            ct);
    }
}