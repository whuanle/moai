// <copyright file="QueryWikiDocumentTaskListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Documents.Queries.Responses;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询文档任务列表.
/// </summary>
public class QueryWikiDocumentTaskListCommand : IRequest<IReadOnlyCollection<WikiDocumentTaskItem>>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }
}
