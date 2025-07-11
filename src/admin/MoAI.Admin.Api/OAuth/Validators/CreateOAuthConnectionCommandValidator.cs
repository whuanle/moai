// <copyright file="CreateOAuthConnectionCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// CreateOAuthConnectionCommandValidator.
/// </summary>
public class CreateOAuthConnectionCommandValidator : Validator<CreateOAuthConnectionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOAuthConnectionCommandValidator"/> class.
    /// </summary>
    public CreateOAuthConnectionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("不能为空.")
            .Length(2, 20).WithMessage("2 - 20 个字符")
            .Matches(@"^[\u4e00-\u9fa5a-zA-Z0-9]+$").WithMessage("只能包含中文、英文和数字.");

        RuleFor(x => x.Key).NotEmpty().WithMessage("不能为空.");

        RuleFor(x => x.Secret).NotEmpty().WithMessage("密钥不能为空.");

        RuleFor(x => x.IconUrl).NotEmpty().WithMessage("图标地址不能为空.");
        RuleFor(x => x.WellKnown).NotEmpty().WithMessage("发现端口不能为空.");
    }
}
