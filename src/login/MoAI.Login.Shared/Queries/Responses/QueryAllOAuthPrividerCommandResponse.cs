// <copyright file="QueryAllOAuthPrividerCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Login.Queries.Responses;

/// <summary>
/// QueryAllOAuthPrividerCommandResponse。
/// </summary>
public class QueryAllOAuthPrividerCommandResponse
{
    /// <summary>
    /// 集合.
    /// </summary>
    public IReadOnlyCollection<QueryAllOAuthPrividerCommandResponseItem> Items { get; init; } = new List<QueryAllOAuthPrividerCommandResponseItem>();
}
