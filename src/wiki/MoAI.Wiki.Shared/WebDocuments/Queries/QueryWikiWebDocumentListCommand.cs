// <copyright file="QueryWikiWebDocumentListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.WebDocuments.Queries;

/// <summary>
/// 查询 web 文档列表.
/// </summary>
public class QueryWikiWebDocumentListCommand : PagedParamter, IRequest<QueryWikiDocumentListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// web 爬虫 id.
    /// </summary>
    public int WikiWebConfigId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; } = default!;
}
