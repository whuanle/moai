// <copyright file="RefreshMcpServerPluginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 刷新 MCP 服务器的工具列表.
/// </summary>
public class RefreshMcpServerPluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    ///  插件 id.
    /// </summary>
    public int PluginId { get; init; }
}
