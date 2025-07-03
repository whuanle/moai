// <copyright file="UpdateOAuthConnectionCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 更新 OAuth2.0 连接配置.
/// </summary>
public class UpdateOAuthConnectionCommand : CreateOAuthConnectionCommand, IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// id.
    /// </summary>
    public int OAuthConnectionId { get; init; }
}
