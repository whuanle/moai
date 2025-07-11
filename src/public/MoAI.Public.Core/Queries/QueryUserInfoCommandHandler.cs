// <copyright file="QueryUserInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;

namespace MoAI.Public.Queries;

/// <summary>
/// 处理查询用户信息的命令.
/// </summary>
public class QueryUserInfoCommandHandler : IRequestHandler<QueryUserInfoCommand, UserStateInfo>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public QueryUserInfoCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<UserStateInfo> Handle(QueryUserInfoCommand request, CancellationToken cancellationToken)
    {
        var queryResult = await _mediator.Send(new QueryUserStateCommand { UserId = request.UserId });
        return queryResult;
    }
}
