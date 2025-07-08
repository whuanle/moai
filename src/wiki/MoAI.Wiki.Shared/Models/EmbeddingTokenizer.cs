// <copyright file="EmbeddingTokenizer.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Wiki.Models;

public enum EmbeddingTokenizer
{
    [JsonPropertyName("p50k")]
    P50k = 0,

    [JsonPropertyName("cl100k")]
    Cl100k = 1,

    [JsonPropertyName("o200k")]
    O200k = 2
}