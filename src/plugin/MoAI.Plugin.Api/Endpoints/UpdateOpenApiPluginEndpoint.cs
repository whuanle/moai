// <copyright file="UpdateOpenApiPluginEndpoint.cs" company="MoAI">
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
using MoAI.Plugin.Queries;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 修改 openapi 插件.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_openapi")]
public class UpdateOpenApiPluginEndpoint : Endpoint<UpdateOpenApiPluginCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOpenApiPluginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateOpenApiPluginEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateOpenApiPluginCommand req, CancellationToken ct)
    {
        var isCreator = await _mediator.Send(new QueryPluginCreatorCommand
        {
            PluginId = req.PluginId,
        });

        if (!isCreator.Exist == false)
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

        return await _mediator.Send(req, ct);
    }
}
