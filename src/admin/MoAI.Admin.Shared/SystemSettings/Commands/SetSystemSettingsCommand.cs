// <copyright file="SetSystemSettingsCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.SystemSettings.Commands;

/// <summary>
/// 设置系统配置.
/// </summary>
public class SetSystemSettingsCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 系统配置.
    /// </summary>
    public KeyValueString Settings { get; init; } = default!;
}
