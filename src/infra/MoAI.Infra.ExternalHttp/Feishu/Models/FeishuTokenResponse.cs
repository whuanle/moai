// <copyright file="FeishuTokenResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuTokenResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }

    [JsonPropertyName("scope")]
    public required string Scope { get; set; }

    [JsonPropertyName("error")]
    public required string Error { get; set; }

    [JsonPropertyName("error_description")]
    public required string ErrorDescription { get; set; }
}
