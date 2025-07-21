// <copyright file="QueryPluginBaseListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.QueryEndpoints;

/// <summary>
/// 查询系统插件简要信息列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/system_plugin_list")]
public class QuerySystemPluginBaseListEndpoint : Endpoint<QuerySystemPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemPluginBaseListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QuerySystemPluginBaseListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginBaseListCommandResponse> ExecuteAsync(QuerySystemPluginBaseListCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            UserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(
            new QuerySystemPluginBaseListCommand
            {
                Name = req.Name,
                Type = req.Type
            },
            ct);
    }
}