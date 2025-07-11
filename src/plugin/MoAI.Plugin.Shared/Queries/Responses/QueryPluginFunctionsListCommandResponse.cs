// <copyright file="QueryPluginFunctionsListCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Plugin.Queries.Responses;

/// <summary>
/// 函数列表.
/// </summary>
public class QueryPluginFunctionsListCommandResponse
{
    /// <summary>
    /// 函数列表.
    /// </summary>
    public IReadOnlyCollection<QueryPluginFunctionsListCommandResponseItem> Items { get; init; } = Array.Empty<QueryPluginFunctionsListCommandResponseItem>();
}
