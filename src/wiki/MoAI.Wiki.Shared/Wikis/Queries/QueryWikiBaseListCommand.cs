// <copyright file="QueryWikiBaseListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询当前用户可以看到的所有知识库.
/// </summary>
public class QueryWikiBaseListCommand : IRequest<IReadOnlyCollection<QueryWikiInfoResponse>>
{
    /// <summary>
    /// 用户id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 是否系统知识库.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 只查询该用户创建的知识库.
    /// </summary>
    public bool? IsOwn { get; init; }

    /// <summary>
    /// 只查询公开的知识库.
    /// </summary>
    public bool? IsPublic { get; init; }

    /// <summary>
    /// 只查询已加入的知识库.
    /// </summary>
    public bool? IsUser { get; init; }
}