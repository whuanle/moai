// <copyright file="SearchWikiDocumentTextCommand.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 搜索知识库文档分片.
/// </summary>
public class SearchWikiDocumentTextCommand : IRequest<SearchWikiDocumentTextCommandResponse>
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 文档id，不设置时搜索整个知识库.
    /// </summary>
    public int? DocumentId { get; set; }

    /// <summary>
    /// 搜索文本，区配文本.
    /// </summary>
    public string? Query { get; init; }
}
