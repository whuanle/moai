// <copyright file="UpdateUserPasswordCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Shared.Commands;

/// <summary>
/// 重置密码.
/// </summary>
public class UpdateUserPasswordCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 新的密码，提前使用 rsa 公有加密.
    /// </summary>
    public string Password { get; init; } = default!;
}