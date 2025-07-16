// <copyright file="CreateWikiCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Validators;

/// <summary>
/// InviteWikiUserCommandValidator
/// </summary>
public class InviteWikiUserCommandValidator : Validator<InviteWikiUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InviteWikiUserCommandValidator"/> class.
    /// </summary>
    public InviteWikiUserCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("被邀请用户信息错误");
    }
}
