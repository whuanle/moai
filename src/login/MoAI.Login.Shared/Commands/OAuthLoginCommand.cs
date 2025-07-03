// <copyright file="OAuthLoginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Login.Commands.Responses;

namespace MoAI.Login.Commands;

/// <summary>
/// 使用 OAuth 登录.
/// </summary>
public class OAuthLoginCommand : IRequest<OAuthLoginCommandResponse>
{
    /// <summary>
    /// Code.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public string OAuthId { get; init; } = default!;
}

public class OAuthLoginCommandResponse
{
    /// <summary>
    /// 如果已经绑定用户.
    /// </summary>
    public bool IsBindUser { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public string OAuthId { get; init; } = default!;

    /// <summary>
    /// 如果此 id 没有绑定过用户，则返回此 id，使用此 id 绑定或注册用户.
    /// </summary>
    public string? OAuthBindId { get; init; }

    /// <summary>
    /// 用户在第三方登录的名字.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// IsBindUser == true 时，返回用户登录信息.
    /// </summary>
    public LoginCommandResponse? LoginCommandResponse { get; init; } = default!;
}