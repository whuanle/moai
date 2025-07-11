// <copyright file="DisableUserEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin.User.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Queries;
using MoAI.Public.Queries;

namespace MoAI.Admin.User.Endpoints;

/// <summary>
/// 禁用用户.
/// </summary>
[HttpPost($"{ApiPrefix.User}/disable_user")]
public class DisableUserEndpoint : Endpoint<DisableUserCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisableUserEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DisableUserEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DisableUserCommand req, CancellationToken ct)
    {
        // 用户里面有管理员
        if (req.UserIds.Any(id => id == _userContext.UserId))
        {
            throw new BusinessException("不能操作自己") { StatusCode = 403 };
        }

        // 用户是否管理员
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            // 不是管理员
            throw new BusinessException("用户没有权限操作") { StatusCode = 403 };
        }

        if (!isAdmin.IsRoot)
        {
            var anyAdmin = await _mediator.Send(new QueryAnyUserIsAdminCommand { UserIds = req.UserIds }, ct);

            if (anyAdmin.Value)
            {
                throw new BusinessException("管理员用户只有超级管理员可以操作") { StatusCode = 403 };
            }
        }

        return await _mediator.Send(req, ct);
    }
}