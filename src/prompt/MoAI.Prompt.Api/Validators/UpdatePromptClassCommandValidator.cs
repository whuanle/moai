// <copyright file="UpdatePromptClassCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FluentValidation;
using MaomiAI.Prompt.Commands;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.Validators;

/// <summary>
/// UpdatePromptClassCommandValidator.
/// </summary>
public class UpdatePromptClassCommandValidator : AbstractValidator<UpdatePromptClassCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptClassCommandValidator"/> class.
    /// </summary>
    public UpdatePromptClassCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("分类ID不能为空");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(20).WithMessage("名称不能超过20个字符");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("描述不能为空")
            .MaximumLength(255).WithMessage("描述不能超过255个字符");
    }
}
