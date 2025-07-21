// <copyright file="DeleteAiAssistantChatCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Queries;

namespace MoAI.App.AIAssistant.Validators;

/// <summary>
/// QueryAiAssistantChatHistoryCommandValidator.
/// </summary>
public class QueryAiAssistantChatHistoryCommandValidator : Validator<QueryUserViewAiAssistantChatHistoryCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiAssistantChatHistoryCommandValidator"/> class.
    /// </summary>
    public QueryAiAssistantChatHistoryCommandValidator()
    {
        RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id不正确.")
            .Must(x => x != Guid.Empty).WithMessage("对话id不正确.");
    }
}
