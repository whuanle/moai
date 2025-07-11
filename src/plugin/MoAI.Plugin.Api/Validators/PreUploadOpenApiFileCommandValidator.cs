// <copyright file="PreUploadOpenApiFileCommandValidator.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using FastEndpoints;
using FluentValidation;
using MaomiAI.Plugin.Shared.Commands;
using MaomiAI.Store.Commands;

namespace MaomiAI.Store.Validators;

public class PreUploadOpenApiFileCommandValidator : Validator<PreUploadOpenApiFileCommand>
{
    public PreUploadOpenApiFileCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("团队id不能为空.");
        RuleFor(x => x.FileName).NotEmpty().Length(2, 50);
        RuleFor(x => x.ContentType).NotEmpty().Length(2, 50);
        RuleFor(x => x.FileSize).NotEmpty();
        RuleFor(x => x.MD5).NotEmpty().Length(2, 50);
    }
}
