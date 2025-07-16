// <copyright file="OpenAIChatCompletionsObject.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CA1720 // 标识符包含类型名称

using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 对话结束后的对象.
/// </summary>
public class OpenAIChatCompletionsObject : IOpenAIChatCompletionsObject
{
    /// <summary>
    /// 聊天完成的唯一标识符
    /// </summary>
    [JsonPropertyName("id")]
    public virtual string Id { get; init; } = default!;

    /// <summary>
    /// 创建聊天完成的Unix时间戳(秒)
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; init; } = default!;

    /// <summary>
    /// 用于聊天完成的模型
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = default!;

    /// <summary>
    /// 该指纹表示模型运行的后端配置
    /// </summary>
    [JsonPropertyName("system_fingerprint")]
    public string SystemFingerprint { get; init; } = default!;

    /// <summary>
    /// 聊天完成选项列表。如果n大于1,可以有多个选项.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyCollection<OpenAIChatCompletionsChoice> Choices { get; init; } = Array.Empty<OpenAIChatCompletionsChoice>();

    /// <summary>
    /// 完成请求的使用统计信息
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAIChatCompletionsUsage Usage { get; init; } = new OpenAIChatCompletionsUsage();

    /// <summary>
    /// 对象类型,总是 chat.completion.
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; } = "chat.completion";
}