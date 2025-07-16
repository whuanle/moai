// <copyright file="ProcessingAiAssistantChatCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AI.Models;
using MoAI.App.AIAssistant.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 进行对话，对话时，History 每次做增量传递.
/// </summary>
public class ProcessingAiAssistantChatCommand : AIAssistantChatObject, IStreamRequest<IOpenAIChatCompletionsObject>
{
    /// <summary>
    /// 对话 id，id 为空时自动新建.
    /// </summary>
    public Guid? ChatId { get; init; } = default!;

    /// <summary>
    /// 当前用户标识.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 用户的提问.
    /// </summary>
    public string Content { get; init; } = string.Empty;
}
