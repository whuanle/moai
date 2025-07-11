// <copyright file="UpdateOAuthConnectionlEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Common.Queries;

namespace MoAI.Admin.OAuth.Endpoints;

/// <summary>
/// 更新 OAuth2.0 连接配置.
/// </summary>
[HttpPut($"{ApiPrefix.OAuth}/update")]
public class UpdateOAuthConnectionlEndpoint : Endpoint<UpdateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOAuthConnectionlEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateOAuthConnectionlEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateOAuthConnectionCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}