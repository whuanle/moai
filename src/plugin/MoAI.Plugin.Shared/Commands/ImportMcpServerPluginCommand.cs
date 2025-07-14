// <copyright file="ImportMcpServerPluginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 导入 mcp 服务.
/// </summary>
public class ImportMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<SimpleInt>
{
    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}
