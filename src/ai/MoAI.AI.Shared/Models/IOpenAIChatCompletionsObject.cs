// <copyright file="IOpenAIChatCompletionsObject.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CA1720 // 标识符包含类型名称

using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 对话响应结果抽象，聊天对象块.
/// </summary>
public interface IOpenAIChatCompletionsObject
{
    /// <summary>
    /// 聊天完成的唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    /// <summary>
    /// 创建聊天完成的Unix时间戳(秒)
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; }

    /// <summary>
    /// 用于聊天完成的模型
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; }

    /// <summary>
    /// 该指纹表示模型运行的后端配置
    /// </summary>
    [JsonPropertyName("system_fingerprint")]
    public string SystemFingerprint { get; }

    /// <summary>
    /// 对象类型,总是 chat.completion
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; }
}
