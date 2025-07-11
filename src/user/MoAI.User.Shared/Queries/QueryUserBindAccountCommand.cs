// <copyright file="QueryUserBindAccountCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.User.Queries.Responses;

namespace MoAI.User.Queries;

/// <summary>
/// 查询用户绑定的第三方账号.
/// </summary>
public class QueryUserBindAccountCommand : IRequest<QueryUserBindAccountCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
