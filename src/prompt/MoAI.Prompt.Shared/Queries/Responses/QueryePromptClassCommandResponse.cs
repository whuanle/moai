// <copyright file="QueryePromptClassCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Prompt.Queries.Responses;

public class QueryePromptClassCommandResponse
{
    public IReadOnlyCollection<QueryePromptClassCommandResponseItem> Items { get; init; } = new List<QueryePromptClassCommandResponseItem>();
}
