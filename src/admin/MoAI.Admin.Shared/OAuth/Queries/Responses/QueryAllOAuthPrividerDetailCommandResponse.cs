// <copyright file="QueryAllOAuthPrividerDetailCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Login.Queries;

namespace MoAI.Admin.OAuth.Queries.Responses;

/// <summary>
/// <inheritdoc cref="QueryAllOAuthPrividerDetailCommand"/>
/// </summary>
public class QueryAllOAuthPrividerDetailCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<QueryAllOAuthPrividerDetailCommandResponseItem> Items { get; init; } = Array.Empty<QueryAllOAuthPrividerDetailCommandResponseItem>();
}
