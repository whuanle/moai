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
    [Description("自定义")]
    Custom = 0,

    /// <summary>
    /// 飞书.
    /// </summary>
    [JsonPropertyName("feishu")]
    [Description("飞书")]
    Feishu = 1,

    /// <summary>
    /// 企业微信.
    /// </summary>
    [JsonPropertyName("weixinwork")]
    [Description("企业微信")]
    WeixinWork = 2,

    /// <summary>
    /// GitHub.
    /// </summary>
    [JsonPropertyName("github")]
    [Description("Github")]
    GitHub = 3,
}
