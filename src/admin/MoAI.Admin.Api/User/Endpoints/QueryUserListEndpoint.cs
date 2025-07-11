// <copyright file="QueryUserListEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MediatR;
using MoAI.Admin.User.Queries;
using MoAI.Admin.User.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Common.Queries;

namespace MoAI.Admin.User.Endpoints;

/// <summary>
/// 查询用户列表.
/// </summary>
[HttpPost($"{ApiPrefix.User}/user_list")]
public class QueryUserListEndpoint : Endpoint<QueryUserListCommand, QueryUserListCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryUserListCommandResponse> ExecuteAsync(QueryUserListCommand req, CancellationToken ct)
    {
        // 用户是否管理员
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { UserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            // 不是管理员
            throw new BusinessException("用户没有权限操作") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
