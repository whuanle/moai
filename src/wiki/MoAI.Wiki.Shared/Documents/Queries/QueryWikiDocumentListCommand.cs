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
public class QueryWikiDocumentListCommand : PagedParamter, IRequest<QueryWikiDocumentListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; } = default!;

    /// <summary>
    /// 包括文件类型，如 .md、.docx 等.
    /// </summary>
    public IReadOnlyCollection<string> IncludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排除文件类型，如 .md、.docx 等.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFileTypes { get; init; } = Array.Empty<string>();
}
