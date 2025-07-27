// <copyright file="QueryePromptClassCommandResponseItem.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Prompt.Queries.Responses;

public class QueryePromptClassCommandResponseItem
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;
}