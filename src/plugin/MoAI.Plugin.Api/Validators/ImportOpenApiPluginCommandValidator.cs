// <copyright file="ImportOpenApiPluginCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// ImportOpenApiPluginCommandValidator.
/// </summary>
public class ImportOpenApiPluginCommandValidator : Validator<ImportOpenApiPluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiPluginCommandValidator"/> class.
    /// </summary>
    public ImportOpenApiPluginCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-20 之间.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间.")
            .Matches("^[a-zA-Z]+$").WithMessage("插件名称只能包含字母.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件名称不能为空.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        RuleFor(x => x.FileId)
            .GreaterThan(0).WithMessage("文件 ID 必须大于 0.");
    }
}
