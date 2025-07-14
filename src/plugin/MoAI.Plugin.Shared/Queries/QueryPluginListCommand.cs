// <copyright file="QueryPluginListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 获取插件列表.
/// </summary>
public class QueryPluginListCommand : IRequest<QueryPluginListCommandResponse>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 筛选类型.
    /// </summary>
    public PluginType? Type { get; init; }

    ///// <summary>
    ///// 插件列表.
    ///// </summary>
    //public IReadOnlyCollection<int>? PluginIds { get; init; } = Array.Empty<int>();
}
