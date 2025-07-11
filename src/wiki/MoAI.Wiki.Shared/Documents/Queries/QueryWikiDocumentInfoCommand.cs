// <copyright file="QueryWikiDocumentInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询单个文档信息.
/// </summary>
public class QueryWikiDocumentInfoCommand : IRequest<QueryWikiDocumentListItem>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; } = default!;
}