// <copyright file="QueryUserStateCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Queries;

/// <summary>
/// 查询用户状态和信息.
/// </summary>
public class QueryUserStateCommand : IRequest<UserStateInfo>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
