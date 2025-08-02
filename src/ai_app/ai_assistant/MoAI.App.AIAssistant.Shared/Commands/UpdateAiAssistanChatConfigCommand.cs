// <copyright file="UpdateAiAssistanChatConfigCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 更新 AI 对话参数.
/// </summary>
public class UpdateAiAssistanChatConfigCommand : AIAssistantChatObject, IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// AI 的头像.
    /// </summary>
    public string AiAvatar { get; init; } = string.Empty;
}
