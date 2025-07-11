// <copyright file="PreUploadOpenApiFilePluginCommandVidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// PreUploadOpenApiFilePluginCommandVidator.
/// </summary>
public class PreUploadOpenApiFilePluginCommandVidator : Validator<PreUploadOpenApiFilePluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadOpenApiFilePluginCommandVidator"/> class.
    /// </summary>
    public PreUploadOpenApiFilePluginCommandVidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称不能为空.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不能为空.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于0.");

        RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件 MD5 不能为空.");
    }
}