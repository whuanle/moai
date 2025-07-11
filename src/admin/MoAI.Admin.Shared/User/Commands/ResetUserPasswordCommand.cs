// <copyright file="ResetUserPasswordCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 重置用户密码.
/// </summary>
public class ResetUserPasswordCommand : IRequest<SimpleString>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
