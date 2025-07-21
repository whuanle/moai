// <copyright file="QueryUserViewUserInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;

namespace MoAI.Common.Queries;

/// <summary>
/// 处理查询用户信息的命令.
/// </summary>
public class QueryUserViewUserInfoCommandHandler : IRequestHandler<QueryUserViewUserInfoCommand, UserStateInfo>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserViewUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserViewUserInfoCommandHandler(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> Handle(QueryUserViewUserInfoCommand request, CancellationToken cancellationToken)
    {
        var queryResult = await _mediator.Send(new QueryUserStateCommand { UserId = _userContext.UserId });
        return queryResult;
    }
}
