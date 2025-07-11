// <copyright file="QueryPluginInfoItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Plugin.Models;

namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// 插件信息.
/// </summary>
public class QueryPluginInfoItem
{
    /// <summary>
    /// 插件Id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; init; } = default!;

    /// <summary>
    /// system|mcp|openapi.
    /// </summary>
    public PluginType Type { get; set; }

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;
}
