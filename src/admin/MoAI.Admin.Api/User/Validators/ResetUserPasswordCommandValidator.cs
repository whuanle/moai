// <copyright file="ResetUserPasswordCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Admin.SystemSettings.Commands;
using MoAI.Admin.User.Commands;
using MoAI.Login.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// ResetUserPasswordCommandValidator.
/// </summary>
public class ResetUserPasswordCommandValidator : Validator<ResetUserPasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResetUserPasswordCommandValidator"/> class.
    /// </summary>
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("用户ID必须大于0")
            .WithMessage("用户ID不能为空");
    }
}