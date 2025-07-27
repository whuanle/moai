// <copyright file="OAuthLoginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 使用 OAuth 登录，用于第三方登录回调后触发接口.
/// </summary>
public class OAuthLoginCommand : IRequest<OAuthLoginCommandResponse>
{
    /// <summary>
    /// Code.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <summary>
    /// id.
    /// </summary>
    public Guid OAuthId { get; init; } = default!;
}
