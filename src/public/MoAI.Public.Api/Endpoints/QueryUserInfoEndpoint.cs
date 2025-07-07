// <copyright file="QueryUserInfoEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Login.Queries.Responses;
using MoAI.Public.Queries;
using MoAI.Public.Queries.Response;

namespace MoAI.Public.Endpoints;

/// <summary>
/// 查询用户基本信息.
/// </summary>
[HttpGet($"{ApiPrefix.Prefix}/userinfo")]
public class QueryUserInfoEndpoint : EndpointWithoutRequest<UserStateInfo>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserInfoEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserInfoEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override Task<UserStateInfo> ExecuteAsync(CancellationToken ct)
    {
        var query = new QueryUserInfoCommand
        {
            UserId = _userContext.UserId
        };

        return _mediator.Send(query, ct);
    }
}
