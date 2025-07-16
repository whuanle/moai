// <copyright file="QueryAiAssistantChatTopicListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.App.AIAssistant.Models;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// 查询用户的 AI 助手对话主题列表.
/// </summary>
public class QueryAiAssistantChatTopicListCommand : IRequest<QueryAiAssistantChatTopicListCommandResponse>
{
    /// <summary>
    /// 当前用户标识.
    /// </summary>
    public int UserId { get; init; } = default!;
}
