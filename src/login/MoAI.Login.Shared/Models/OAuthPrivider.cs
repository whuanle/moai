// <copyright file="OAuthPrivider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Login.Models;

/// <summary>
/// OAuth 提供商.
/// </summary>
public enum OAuthPrivider
{
    /// <summary>
    /// 自定义.
    /// </summary>
    Custom = 0,

    /// <summary>
    /// 飞书.
    /// </summary>
    Feishu = 1,

    /// <summary>
    /// GitHub.
    /// </summary>
    GitHub = 2,

    /// <summary>
    /// GitLab.
    /// </summary>
    GitLab = 3,

    /// <summary>
    /// Gitee.
    /// </summary>
    Gitee = 4,

    /// <summary>
    /// 微信.
    /// </summary>
    WeChat = 5
}
