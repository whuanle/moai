// <copyright file="DeleteAiAssistantChatCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 删除对话记录.
/// </summary>
public class DeleteAiAssistantChatCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 对话 id.
    /// </summary>
    public string ChatId { get; init; } = default!;
}