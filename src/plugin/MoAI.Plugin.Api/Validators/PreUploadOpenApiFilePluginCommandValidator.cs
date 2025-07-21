// <copyright file="PreUploadOpenApiFilePluginCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// PreUploadOpenApiFilePluginCommandValidator.
/// </summary>
public class PreUploadOpenApiFilePluginCommandValidator : Validator<PreUploadOpenApiFilePluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadOpenApiFilePluginCommandValidator"/> class.
    /// </summary>
    public PreUploadOpenApiFilePluginCommandValidator()
    {
        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不正确.")
            .Length(2, 50).WithMessage("文件类型不正确.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件 MD5 不能为空.")
            .Length(32).WithMessage("文件 MD5 长度必须为 32 位.")
            .Matches("^[a-fA-F0-9]{32}$").WithMessage("文件 MD5 格式不正确.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于 0.")
            .LessThanOrEqualTo(1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
    }
}