// <copyright file="WorkflowFieldType.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models.Abact;

public enum WorkflowFieldType
{
    [JsonPropertyName("string")]
    String,

    [JsonPropertyName("number")]
    Number,

    [JsonPropertyName("boolean")]
    Boolean,

    [JsonPropertyName("integer")]
    Integer,

    [JsonPropertyName("object")]
    Object,

    [JsonPropertyName("array")]
    Array,

    [JsonPropertyName("map")]
    Map
}
