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
        RuleFor(x => x.FileName).NotEmpty().Length(2, 50);
        RuleFor(x => x.ContentType).NotEmpty().Length(2, 50);
        RuleFor(x => x.FileSize).NotEmpty();
        RuleFor(x => x.MD5).NotEmpty().Length(2, 100);
    }
}