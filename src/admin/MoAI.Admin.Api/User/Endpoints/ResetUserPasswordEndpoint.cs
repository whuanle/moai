// <copyright file="ResetUserPasswordEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin.User.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Queries;
using MoAI.Public.Queries;

namespace MoAI.Admin.User.Endpoints;

/// <summary>
/// 重置用户密码.
/// </summary>
[HttpPut($"{ApiPrefix.User}/reset_password")]
public class ResetUserPasswordEndpoint : Endpoint<ResetUserPasswordCommand, SimpleString>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetUserPasswordEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public ResetUserPasswordEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<SimpleString> ExecuteAsync(ResetUserPasswordCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        if (isAdmin.IsRoot)
        {
            return await _mediator.Send(req, ct);
        }

        var userIsAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = req.UserId });

        if (userIsAdmin.IsAdmin)
        {
            throw new BusinessException("只有超级管理员可以操作") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
