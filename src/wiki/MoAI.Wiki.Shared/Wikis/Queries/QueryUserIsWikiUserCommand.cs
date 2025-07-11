// <copyright file="QueryUserIsWikiUserCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询用户是否在知识库协作成员中.
/// </summary>
public class QueryUserIsWikiUserCommand : IRequest<QueryUserIsWikiUserCommandResponse>
{
    public int WikiId { get; init; }

    public int UserId { get; init; }
}
