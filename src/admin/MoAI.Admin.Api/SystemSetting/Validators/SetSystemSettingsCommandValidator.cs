// <copyright file="SetSystemSettingsCommandValidator.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Admin.SystemSettings.Commands;
using MoAI.Login.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// SetSystemSettingsCommandValidator.
/// </summary>
public class SetSystemSettingsCommandValidator : Validator<SetSystemSettingsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetSystemSettingsCommandValidator"/> class.
    /// </summary>
    public SetSystemSettingsCommandValidator()
    {
    }
}
