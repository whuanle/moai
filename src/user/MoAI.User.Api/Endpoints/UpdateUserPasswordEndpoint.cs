// <copyright file="UpdateUserPasswordEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.User.Commands;

namespace MoAI.User.Endpoints;

/// <summary>
/// 更新密码.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/update_password")]
public class UpdateUserPasswordEndpoint : Endpoint<UpdateUserPasswordCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserPasswordEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UpdateUserPasswordEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UpdateUserPasswordCommand req, CancellationToken ct)
    {
        return await _mediator.Send(
            new UpdateUserPasswordCommand
            {
                UserId = _userContext.UserId,
                Password = req.Password
            }, ct);
    }
}
