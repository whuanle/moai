// <copyright file="UpdateUserInfoEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.User.Commands;

namespace MoAI.User.Endpoints;

/// <summary>
/// 更新用户信息.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_user")]
public class UpdateUserInfoEndpoint : Endpoint<UpdateUserInfoCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserInfoEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateUserInfoEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateUserInfoCommand req, CancellationToken ct)
    {
        if (req.UserId != _userContext.UserId)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
