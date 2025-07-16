// <copyright file="QueryAiAssistantChatHistoryCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries.Responses;

/// <summary>
/// 对话记录结果.
/// </summary>
public class QueryAiAssistantChatHistoryCommandResponse : AIAssistantChatContext
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}