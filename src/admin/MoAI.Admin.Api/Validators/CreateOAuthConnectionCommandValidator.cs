// <copyright file="CreateOAuthConnectionCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Admin.Validators;

public class CreateOAuthConnectionCommandValidator : Validator<CreateOAuthConnectionCommand>
{
    public CreateOAuthConnectionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("认证名称不能为空.").
            MinimumLength(2)
            .MaximumLength(20)
            .Matches(@"^[\u4e00-\u9fa5a-zA-Z0-9]+$").WithMessage("认证名称只能包含中文、英文和数字.");

        RuleFor(x => x.Key).NotEmpty().WithMessage("应用key不能为空.");
        RuleFor(x => x.Secret).NotEmpty().WithMessage("密钥不能为空.");
        RuleFor(x => x.IconUrl).NotEmpty().WithMessage("图标地址不能为空.");
        RuleFor(x => x.WellKnown).NotEmpty().WithMessage("发现端口不能为空.");
    }
}
