// <copyright file="QueryUserIsWikiUserCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryUserIsWikiUserCommandResponse
{
    public int WikiId { get; init; }

    public int UserId { get; init; }

    public bool IsWikiUser { get; init; }

    public bool IsWikiRoot { get; init; }
}