// <copyright file="InviteWikiUserCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryWikiUsersCommandResponse
{
    public IReadOnlyCollection<QueryWikiUsersCommandResponseItem> Users { get; init; } = Array.Empty<QueryWikiUsersCommandResponseItem>();
}
