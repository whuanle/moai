// <copyright file="QueryAdminIdsCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Login.Commands;

public class QueryAdminIdsCommandResponse
{
    public int RootId { get; init; }

    public IReadOnlyCollection<int> AdminIds { get; init; } = new List<int>();
}