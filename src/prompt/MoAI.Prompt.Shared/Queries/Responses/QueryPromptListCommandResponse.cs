// <copyright file="QueryPromptListCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAIPrompt.Models;

namespace MoAIPrompt.Queries.Responses;

/// <summary>
/// 提示词列表.
/// </summary>
public class QueryPromptListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PromptItem> Items { get; init; } = new List<PromptItem>();
}
