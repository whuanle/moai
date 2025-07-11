// <copyright file="RefreshUserStateCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 刷新用户状态.
/// </summary>
public class RefreshUserStateCommand : IRequest<UserStateInfo>
{
    public int UserId { get; init; }
}