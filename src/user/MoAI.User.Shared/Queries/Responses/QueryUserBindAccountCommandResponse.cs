// <copyright file="QueryUserBindAccountCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.User.Queries.Responses;

public class QueryUserBindAccountCommandResponse
{
    public IReadOnlyCollection<QueryUserBindAccountCommandResponseItem> Items { get; init; } = Array.Empty<QueryUserBindAccountCommandResponseItem>();
}
