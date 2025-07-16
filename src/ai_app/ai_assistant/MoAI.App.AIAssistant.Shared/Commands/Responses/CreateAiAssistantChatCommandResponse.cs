// <copyright file="CreateAiAssistantChatCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.App.AIAssistant.Commands.Responses;

/// <summary>
/// 对话 id.
/// </summary>
public class CreateAiAssistantChatCommandResponse
{
    /// <summary>
    /// 每个聊天对话都有唯一 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;
}