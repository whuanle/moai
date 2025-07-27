// <copyright file="OAuthBindExistAccountCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 使用 OAuth 绑定已存在的账号.
/// </summary>
public class OAuthBindExistAccountCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid TempOAuthBindId { get; init; } = default!;
}
