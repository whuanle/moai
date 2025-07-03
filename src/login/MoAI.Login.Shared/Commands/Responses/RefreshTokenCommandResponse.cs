// <copyright file="RefreshTokenCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Login.Commands.Responses;

public class RefreshTokenCommandResponse
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 访问令牌.
    /// </summary>
    public string AccessToken { get; set; } = default!;

    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public string RefreshToken { get; set; } = default!;

    /// <summary>
    /// 令牌类型.
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）.
    /// </summary>
    public long ExpiresIn { get; set; }
}