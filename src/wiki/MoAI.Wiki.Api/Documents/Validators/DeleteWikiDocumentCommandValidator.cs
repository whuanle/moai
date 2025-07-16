// <copyright file="CancalWikiDocumentTaskCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Documents.Commands;
using MoAI.Wiki.Documents.Queries;

namespace MoAI.Wiki.Documents.Validators;

/// <summary>
/// DeleteWikiDocumentCommandValidator.
/// </summary>
public class DeleteWikiDocumentCommandValidator : Validator<DeleteWikiDocumentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiDocumentCommandValidator"/> class.
    /// </summary>
    public DeleteWikiDocumentCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}
