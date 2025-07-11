// <copyright file="FeishuTokenRequest.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuTokenRequest
{
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = "authorization_code";

    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public required string ClientSecret { get; set; }

    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; set; }

    [JsonPropertyName("code_verifier")]
    public required string CodeVerifier { get; set; }

    [JsonPropertyName("scope")]
    public required string Scope { get; set; }
}
