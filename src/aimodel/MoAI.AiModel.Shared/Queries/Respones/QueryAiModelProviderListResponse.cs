// <copyright file="QueryAiModelProviderListResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

/// <summary>
/// AI 模型供应商和已添加的ai模型数量列表.
/// </summary>
public class QueryAiModelProviderListResponse
{
    /// <summary>
    /// AI 服务商列表，{ai服务提供商,模型数量}.
    /// </summary>
    public IReadOnlyCollection<QueryAiModelProviderCount> Providers { get; init; } = new List<QueryAiModelProviderCount>();
}
