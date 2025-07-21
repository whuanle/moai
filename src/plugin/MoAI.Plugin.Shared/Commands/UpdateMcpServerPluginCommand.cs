// <copyright file="UpdateMcpServerPluginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 更新 MCP 插件.
/// </summary>
public class UpdateMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<EmptyCommandResponse>
{
    /// <summary>
    ///  插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 系统插件.
    /// </summary>
    public bool IsSystem { get; init; } = default!;

    /// <summary>
    /// 是否公开，系统插件 == true 时才能设置.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}