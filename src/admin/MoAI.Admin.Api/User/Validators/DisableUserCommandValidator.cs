// <copyright file="DisableUserCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Admin.User.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// DisableUserCommandValidator.
/// </summary>
public class DisableUserCommandValidator : Validator<DisableUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisableUserCommandValidator"/> class.
    /// </summary>
    public DisableUserCommandValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("用户ID不能为空")
            .Must(x => x.Count > 0).WithMessage("用户ID不能为空")
            .Must(x => x.All(id => id > 0)).WithMessage("用户ID必须大于0");
    }
}
