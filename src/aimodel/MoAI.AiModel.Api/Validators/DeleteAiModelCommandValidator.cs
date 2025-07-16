// <copyright file="DeleteAiModelCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.AiModel.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// DeleteAiModelCommandValidator.
/// </summary>
public class DeleteAiModelCommandValidator : Validator<DeleteAiModelCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiModelCommandValidator"/> class.
    /// </summary>
    public DeleteAiModelCommandValidator()
    {
        RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("模型id有误");
    }
}
