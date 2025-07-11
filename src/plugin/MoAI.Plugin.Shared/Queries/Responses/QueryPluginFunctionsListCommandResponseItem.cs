// <copyright file="QueryPluginFunctionsListCommandResponseItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// 函数.
/// </summary>
public class QueryPluginFunctionsListCommandResponseItem
{
    /// <summary>
    /// id.
    /// </summary>
    public int FunctionId { get; set; }

    /// <summary>
    /// 插件路径.
    /// </summary>
    public int PluginId { get; set; }

    /// <summary>
    /// 函数名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Summary { get; set; } = default!;

    /// <summary>
    /// api路径.
    /// </summary>
    public string Path { get; set; } = default!;
}