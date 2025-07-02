﻿// <copyright file="QueryRepeatedUserNameCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Login.Queries;

namespace MoAI.Login.Validators;

/// <inheritdoc/>
public class QueryRepeatedUserNameCommandValidator : Validator<QueryRepeatedUserNameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRepeatedUserNameCommandValidator"/> class.
    /// </summary>
    public QueryRepeatedUserNameCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MinimumLength(5).MaximumLength(20).WithMessage("长度 5-30 字符.");
    }
}
