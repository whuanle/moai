// <copyright file="DeletePluginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 删除插件.
/// </summary>
public class DeletePluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件.
    /// </summary>
    public int PluginId { get; init; }
}