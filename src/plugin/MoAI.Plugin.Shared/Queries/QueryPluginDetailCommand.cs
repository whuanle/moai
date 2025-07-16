// <copyright file="QueryPluginDetailCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 查询该插件的详细信息.
/// </summary>
public class QueryPluginDetailCommand : IRequest<QueryPluginDetailCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; } = default!;
}
