// <copyright file="UpdateUserAiModelRequestValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.AiModel.Commands;
using MoAI.AiModel.User.Models;

namespace MoAI.AiModel.Validators;

/// <summary>
/// UpdateAiModelCommandValidator.
/// </summary>
public class UpdateUserAiModelRequestValidator : Validator<UpdateUserAiModelRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserAiModelRequestValidator"/> class.
    /// </summary>
    public UpdateUserAiModelRequestValidator()
    {
        RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("模型id有误");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("模型名称不能为空")
            .MaximumLength(50).WithMessage("模型名称不能超过50个字符");

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("密钥不能为空");

        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("请求端点不能为空")
            .MaximumLength(500).WithMessage("请求端点不能超过255个字符");
    }
}
