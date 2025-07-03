// <copyright file="DeleteOAuthConnectionCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Admin.Validators;

public class DeleteOAuthConnectionCommandValidator : Validator<DeleteOAuthConnectionCommand>
{
    public DeleteOAuthConnectionCommandValidator()
    {
        RuleFor(x => x.OAuthConnectionId).NotEmpty().WithMessage("ID不能为空.");
    }
}
