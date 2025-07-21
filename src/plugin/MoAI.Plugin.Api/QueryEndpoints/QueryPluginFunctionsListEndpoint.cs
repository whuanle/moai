// <copyright file="QueryPluginFunctionsListEndpoint.cs" company="MoAI">
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
using MoAI.Common.Queries;

namespace MoAI.Plugin.QueryEndpoints;

/// <summary>
/// 插件的函数列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/function_list")]
public class QueryPluginFunctionsListEndpoint : Endpoint<QueryPluginFunctionsListCommand, QueryPluginFunctionsListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginFunctionsListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryPluginFunctionsListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryPluginFunctionsListCommandResponse> ExecuteAsync(QueryPluginFunctionsListCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryPluginCreatorCommand
        {
            PluginId = req.PluginId,
        });

        if (isCreator.Exist == false)
        {
            throw new BusinessException("未找到插件.");
        }

        if (isCreator.IsSystem)
        {
            var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
            {
                UserId = _userContext.UserId
            });

            if (!isAdmin.IsAdmin)
            {
                throw new BusinessException("没有操作权限.") { StatusCode = 403 };
            }
        }
        else if (isCreator.CreatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到插件.") { StatusCode = 404 };
        }


        return await _mediator.Send(
            new QueryPluginFunctionsListCommand
            {
                PluginId = req.PluginId
            },
            ct);
    }
}
