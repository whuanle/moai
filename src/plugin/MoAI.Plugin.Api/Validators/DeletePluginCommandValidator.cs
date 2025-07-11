// <copyright file="DeletePluginCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// DeletePluginCommandValidator.
/// </summary>
public class DeletePluginCommandValidator : Validator<DeletePluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePluginCommandValidator"/> class.
    /// </summary>
    public DeletePluginCommandValidator()
    {
        RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件id不能为空.")
            .GreaterThan(0).WithMessage("插件id必须大于0.");
    }
}
