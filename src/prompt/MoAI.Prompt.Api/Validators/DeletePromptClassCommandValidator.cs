// <copyright file="DeletePromptClassCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FluentValidation;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.Validators;

/// <summary>
/// DeletePromptClassCommandValidator.
/// </summary>
public class DeletePromptClassCommandValidator : AbstractValidator<DeletePromptClassCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptClassCommandValidator"/> class.
    /// </summary>
    public DeletePromptClassCommandValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty().WithMessage("分类ID不能为空");
    }
}
