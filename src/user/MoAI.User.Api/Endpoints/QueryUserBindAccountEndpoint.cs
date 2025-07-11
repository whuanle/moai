// <copyright file="UploadtUserAvatarEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.User.Queries;
using MoAI.User.Queries.Responses;
using MoAI.User.Shared.Commands;

namespace MoAI.User.Endpoints;

/// <summary>
/// 查询用户已经绑定的第三方账号.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/oauth_list")]
public class QueryUserBindAccountEndpoint : EndpointWithoutRequest<QueryUserBindAccountCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserBindAccountEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserBindAccountEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    public async Task<QueryUserBindAccountCommandResponse> Handle(CancellationToken cancellationToken)
    {
        return await _mediator.Send(new QueryUserBindAccountCommand
        {
            UserId = _userContext.UserId
        }, cancellationToken);
    }
}