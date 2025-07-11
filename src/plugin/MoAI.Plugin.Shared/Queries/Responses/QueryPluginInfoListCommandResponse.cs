// <copyright file="QueryPluginInfoListCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Plugin.Queries.Responses;

public class QueryPluginInfoListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<QueryPluginInfoItem> Items { get; init; } = Array.Empty<QueryPluginInfoItem>();
}