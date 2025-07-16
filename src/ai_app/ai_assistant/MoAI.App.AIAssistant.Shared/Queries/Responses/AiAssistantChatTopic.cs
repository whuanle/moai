// <copyright file="QueryAiAssistantChatHistoryCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries.Responses;

public class AiAssistantChatTopic
{
    public Guid ChatId { get; init; } = default!;
    public string Title { get; init; } = default!;
    public DateTimeOffset CreateTime { get; init; } = default!;
}
