// <copyright file="QueryAiAssistantChatHistoryCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.App.AIAssistant.Queries.Responses;

namespace MoAI.App.AIAssistant.Queries;

/// <summary>
/// 查询对话记录.
/// </summary>
public class QueryUserViewAiAssistantChatHistoryCommand : IRequest<QueryAiAssistantChatHistoryCommandResponse>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;
}
