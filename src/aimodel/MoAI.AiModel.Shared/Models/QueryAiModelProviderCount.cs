// <copyright file="QueryAiModelProviderCount.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.AiModel.Models;

/// <summary>
/// AI 模型数量.
/// </summary>
public class QueryAiModelProviderCount
{
    /// <summary>
    /// 供应商名称.
    /// </summary>
    public string Provider { get; init; } = default!;

    /// <summary>
    /// 模型数量.
    /// </summary>
    public int Count { get; init; }
}