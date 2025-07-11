// <copyright file="QueryWikiDocumentListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询 wiki 文件列表.
/// </summary>
public class QueryWikiDocumentListCommand : PagedParamter, IRequest<QueryWikiDocumentListResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; } = default!;
}
