// <copyright file="QueryWikiWebConfigListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Queries;

/// <summary>
/// 查询知识库爬虫列表.
/// </summary>
public class QueryWikiWebConfigListCommand : IRequest<QueryWikiWebConfigListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }
}

public class QueryWikiWebConfigListCommandResponse
{
    /// <summary>
    /// 爬虫配置列表.
    /// </summary>
    public IReadOnlyCollection<WeikiWebConfigSimpleItem> Items { get; init; } = Array.Empty<WeikiWebConfigSimpleItem>();
}

public class WeikiWebConfigSimpleItem : AuditsInfo
{
    /// <summary>
    /// 爬虫配置 id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 爬虫标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 爬虫地址.
    /// </summary>
    public string Address { get; init; } = default!;
}