// <copyright file="UnbindUserAccountEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.User.Commands;

namespace MoAI.User.OAuthEndpoints;

/// <summary>
/// 解绑第三方账号.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/unbind-oauth")]
public class UnbindUserAccountEndpoint : Endpoint<UnbindUserAccountCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnbindUserAccountEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UnbindUserAccountEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UnbindUserAccountCommand req, CancellationToken ct)
    {
        var command = new UnbindUserAccountCommand
        {
            UserId = _userContext.UserId,
            BindId = req.BindId
        };

        return await _mediator.Send(command, ct);
    }
}