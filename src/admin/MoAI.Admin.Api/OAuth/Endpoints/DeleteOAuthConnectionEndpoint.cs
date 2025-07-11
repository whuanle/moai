﻿// <copyright file="DeleteOAuthConnectionEndpoint.cs" company="MoAI">
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
/// 删除认证方式.
/// </summary>
[HttpDelete($"{ApiPrefix.OAuth}/delete")]
public class DeleteOAuthConnectionEndpoint : Endpoint<DeleteOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteOAuthConnectionEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public DeleteOAuthConnectionEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(DeleteOAuthConnectionCommand req, CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
