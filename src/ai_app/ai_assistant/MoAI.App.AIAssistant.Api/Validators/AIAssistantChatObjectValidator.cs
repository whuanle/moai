// <copyright file="DeleteAiAssistantChatCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Handlers;
using MoAI.App.AIAssistant.Models;
using MoAI.App.AIAssistant.Queries;

namespace MoAI.App.AIAssistant.Validators;

/// <summary>
/// AIAssistantChatObjectValidator.
/// </summary>
public class AIAssistantChatObjectValidator : Validator<AIAssistantChatObject>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIAssistantChatObjectValidator"/> class.
    /// </summary>
    public AIAssistantChatObjectValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("对话标题不能为空.")
            .MaximumLength(100).WithMessage("对话标题长度不能超过100个字符.");

        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("提示词不能为空.")
            .MaximumLength(2000).WithMessage("提示词长度不能超过2000个字符.");

        RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型 ID 错误.");
    }
}
