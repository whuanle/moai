// <copyright file="NodeType.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models.Abact;

/// <summary>
/// 节点类型.
/// </summary>
public enum NodeType
{
    [JsonPropertyName("start")]
    Start,

    [JsonPropertyName("end")]
    End,

    [JsonPropertyName("plugin")]
    Plugin,

    [JsonPropertyName("wiki")]
    Wiki,

    [JsonPropertyName("ai_question")]
    AiQuestion,

    [JsonPropertyName("http")]
    Http,

    [JsonPropertyName("javascript")]
    JavaScript,

    [JsonPropertyName("condition")]
    Condition,

    [JsonPropertyName("fork")]
    Fork,

    [JsonPropertyName("join")]
    Join
}
