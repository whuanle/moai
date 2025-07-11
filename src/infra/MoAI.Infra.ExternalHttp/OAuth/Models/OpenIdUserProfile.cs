// <copyright file="OpenIdUserProfile.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Infra.OAuth.Models;

public class OpenIdUserProfile
{
    [JsonPropertyName("sub")]
    public required string Sub { get; set; }

    [JsonPropertyName("iss")]
    public required string Issuer { get; set; }

    [JsonPropertyName("aud")]
    public required string Audience { get; set; }

    [JsonPropertyName("preferred_username")]
    public required string PreferredUsername { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("picture")]
    public required string Picture { get; set; }
}
