// <copyright file="CreateWikiCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Wikis.Commands;
using MoAI.Wiki.Wikis.Queries;

namespace MoAI.Wiki.Wikis.Validators;

/// <summary>
/// DeleteWikiCommandValidator
/// </summary>
public class DeleteWikiCommandValidator : Validator<DeleteWikiCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiCommandValidator"/> class.
    /// </summary>
    public DeleteWikiCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
