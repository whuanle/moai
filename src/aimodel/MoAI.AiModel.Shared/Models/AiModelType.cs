// <copyright file="AiModelType.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MoAI.AiModel.Models;

/// <summary>
/// AI 模型大分类.
/// </summary>
public enum AiModelType
{
    /// <summary>
    /// chat.
    /// </summary>
    [JsonPropertyName("chat")]
    [EnumMember(Value = "chat")]
    Chat,

    /// <summary>
    /// embedding.
    /// </summary>
    [JsonPropertyName("embedding")]
    [EnumMember(Value = "embedding")]
    Embedding,

    /// <summary>
    /// image.
    /// </summary>
    [JsonPropertyName("image")]
    [EnumMember(Value = "image")]
    Image,

    /// <summary>
    /// tts.
    /// </summary>
    [JsonPropertyName("tts")]
    [EnumMember(Value = "tts")]
    TTS,

    /// <summary>
    /// stts.
    /// </summary>
    [JsonPropertyName("stts")]
    [EnumMember(Value = "stts")]
    STTS,

    /// <summary>
    /// realtime.
    /// </summary>
    [JsonPropertyName("realtime")]
    [EnumMember(Value = "realtime")]
    Realtime,


    /// <summary>
    /// realtime.
    /// </summary>
    [JsonPropertyName("text2video")]
    [EnumMember(Value = "text2video")]
    Text2video,

    /// <summary>
    /// realtime.
    /// </summary>
    [JsonPropertyName("text2music")]
    [EnumMember(Value = "text2music")]
    Text2music,
}
