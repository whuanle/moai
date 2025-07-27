// <copyright file="QueryUserPluginBaseListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// 获取用户插件基础信息列表.
/// </summary>
public class QueryUserPluginBaseListCommand : IRequest<QueryPluginBaseListCommandResponse>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 筛选类型.
    /// </summary>
    public PluginType? Type { get; init; }

    /// <summary>
    /// 只查询该用户创建的插件，不设置时查询所有用户可用插件.
    /// </summary>
    public bool? IsOwn { get; init; }
}
