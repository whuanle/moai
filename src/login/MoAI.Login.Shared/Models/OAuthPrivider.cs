// <copyright file="OAuthPrivider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MoAI.Login.Models;

/// <summary>
/// OAuth 提供商.
/// </summary>
public enum OAuthPrivider
{
    /// <summary>
    /// 自定义.
    /// </summary>
    [JsonPropertyName("custom")]
    Custom = 0,

    /// <summary>
    /// 飞书.
    /// </summary>
    [JsonPropertyName("feishu")]
    Feishu = 1,

    /// <summary>
    /// 钉钉.
    /// </summary>
    [JsonPropertyName("dingtalk")]
    DingTalk = 2,

    /// <summary>
    /// GitHub.
    /// </summary>
    [JsonPropertyName("github")]
    GitHub = 3,
}
