// <copyright file="QueryUserInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Queries.Responses;
using MoAI.Public.Queries.Response;

namespace MoAI.Public.Queries;

/// <summary>
/// 查询用户基本信息的请求.
/// </summary>
public class QueryUserInfoCommand : IRequest<UserStateInfo>
{
    /// <summary>
    /// 用户 ID.
    /// </summary>
    public int UserId { get; init; }
}
