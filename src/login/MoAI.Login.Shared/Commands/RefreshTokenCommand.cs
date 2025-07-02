// <copyright file="RefreshTokenCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 刷新 token.
/// </summary>
public class RefreshTokenCommand : IRequest<RefreshTokenCommandResponse>
{
    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public string RefreshToken { get; init; } = default!;
}
