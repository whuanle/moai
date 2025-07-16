// <copyright file="AIAssistantChatContext.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话上下文，一个完整的聊天上下文.
/// </summary>
public class AIAssistantChatContext : AIAssistantChatObject
{
    /// <summary>
    /// 历史对话或者上下文信息，创建对话时，如果有提示词，则第一个对话就是提示词.
    /// </summary>
    public virtual ChatHistory ChatHistory { get; init; } = new ChatHistory();
}