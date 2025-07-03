// <copyright file="RegisterUserCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Login.Validators;

/// <inheritdoc/>
public class RegisterUserCommandValidator : Validator<RegisterUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserCommandValidator"/> class.
    /// </summary>
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MinimumLength(5).MaximumLength(20).WithMessage("用户名 5-20 字符.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MinimumLength(5).MaximumLength(50).WithMessage("邮箱 5-50 字符.");
        RuleFor(x => x.Password).NotEmpty(); // .MinimumLength(8).MaximumLength(30).Matches("(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$").WithMessage("密码 8-30 长度，并包含数字+字母+特殊字符.");
        RuleFor(x => x.NickName).NotEmpty().MinimumLength(3).MaximumLength(20).WithMessage("昵称 3-20 字符.");
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^(?:\+?1)?\d{10,15}$").WithMessage("手机号格式错误.");
    }
}
