// <copyright file="DeleteAiAssistantChatOneRecordCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Commands;

/// <summary>
/// 删除对话中的一条记录.
/// </summary>
public class DeleteAiAssistantChatOneRecordCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 记录id.
    /// </summary>
    public long RecordId { get; init; }
}
