// <copyright file="UpdateOpenApiPluginCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// UpdateOpenApiPluginCommandValidator.
/// </summary>
public class UpdateOpenApiPluginCommandValidator : Validator<UpdateOpenApiPluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOpenApiPluginCommandValidator"/> class.
    /// </summary>
    public UpdateOpenApiPluginCommandValidator()
    {
        RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件id不能为空.")
            .GreaterThan(0).WithMessage("插件id必须大于0.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称不能为空.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述不能为空.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间");
    }
}