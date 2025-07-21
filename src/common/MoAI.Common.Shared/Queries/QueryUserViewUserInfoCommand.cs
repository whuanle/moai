// <copyright file="QueryUserViewUserInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Common.Queries;

/// <summary>
/// 查询用户基本信息的请求.
/// </summary>
public class QueryUserViewUserInfoCommand : IRequest<UserStateInfo>
{
}
