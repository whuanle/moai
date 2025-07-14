// <copyright file="ProcessingAiAssistantChatCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 进行对话.
/// </summary>
public class ProcessingAiAssistantChatCommand : IStreamRequest<IOpenAIChatCompletionsObject>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public string ChatId { get; init; } = default!;

    /// <summary>
    /// 当前用户标识.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 话题名称.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 要使用的 AI 模型.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 要使用的知识库，如果用户不在知识库用户内，则必须是公开的.
    /// </summary>
    public int? WikiId { get; init; }

    /// <summary>
    /// 要使用的插件 id 列表，用户必须有权使用这些插件.
    /// </summary>
    public IReadOnlyCollection<int> PluginIds { get; init; } = new List<int>();

    /// <summary>
    /// 历史对话或者上下文信息，创建对话时，如果有提示词，则第一个对话就是提示词.
    /// </summary>
    public ChatHistory ChatHistory { get; init; } = new ChatHistory();

    /// <summary>
    /// 配置，字典适配不同的 AI 模型.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();
}
