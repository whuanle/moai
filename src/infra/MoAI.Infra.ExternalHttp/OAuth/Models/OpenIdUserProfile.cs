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
    public string Sub { get; set; }

    [JsonPropertyName("iss")]
    public string Issuer { get; set; }

    [JsonPropertyName("aud")]
    public string Audience { get; set; }

    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("picture")]
    public string Picture { get; set; }
}
