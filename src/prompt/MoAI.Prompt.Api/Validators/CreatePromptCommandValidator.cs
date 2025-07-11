// <copyright file="CreatePromptCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FluentValidation;
using MaomiAI.Prompt.Commands;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.Validators;

/// <summary>
/// CreatePromptCommandValidator.
/// </summary>
public class CreatePromptCommandValidator : AbstractValidator<CreatePromptCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptCommandValidator"/> class.
    /// </summary>
    public CreatePromptCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("名称不能为空")
            .MaximumLength(50).WithMessage("名称不能超过50个字符");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("描述不能为空")
            .MaximumLength(200).WithMessage("描述不能超过200个字符");
        RuleFor(x => x.PromptClassId).NotEmpty().WithMessage("分类ID不能为空");
    }
}
