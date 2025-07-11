// <copyright file="RefreshMcpServerPluginEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MaomiAI.Plugin.Shared.Commands.Responses;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using MoAI.Plugin.Commands;
using MoAI.Public.Queries;

namespace MoAI.Plugin.Endpoints;

/// <summary>
/// 刷新mcp服务器的tool列表.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/refresh_mcp")]
public class RefreshMcpServerPluginEndpoint : Endpoint<RefreshMcpServerPluginCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshMcpServerPluginEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public RefreshMcpServerPluginEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(RefreshMcpServerPluginCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            UserId = _userContext.UserId,
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}