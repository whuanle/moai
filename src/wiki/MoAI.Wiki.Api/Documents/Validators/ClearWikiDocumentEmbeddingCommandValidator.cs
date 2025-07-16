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
/// ClearWikiDocumentEmbeddingCommandValidator.
/// </summary>
public class ClearWikiDocumentEmbeddingCommandValidator : Validator<ClearWikiDocumentEmbeddingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClearWikiDocumentEmbeddingCommandValidator"/> class.
    /// </summary>
    public ClearWikiDocumentEmbeddingCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
