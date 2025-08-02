// <copyright file="QueryAiAssistantChatHistoryCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries.Responses;

/// <summary>
/// 对话记录结果.
/// </summary>
public class QueryAiAssistantChatHistoryCommandResponse
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 话题名称.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 提示词，第一次对话时带上，如果后续不需要修改则不需要再次传递.
    /// </summary>
    public string? Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 要使用的 AI 模型.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 要使用的知识库，如果用户不在知识库用户内，则必须是公开的.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 要使用的插件 id 列表，用户必须有权使用这些插件.
    /// </summary>
    public IReadOnlyCollection<int> PluginIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 配置，字典适配不同的 AI 模型.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// AI 的头像.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 输入token统计.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出token统计.
    /// </summary>
    public int OutTokens { get; set; }

    /// <summary>
    /// 使用的 token 总数.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 历史对话或者上下文信息，创建对话时，如果有提示词，则第一个对话就是提示词.
    /// </summary>
    public virtual IReadOnlyCollection<ChatContentItem> ChatHistory { get; init; } = Array.Empty<ChatContentItem>();
}