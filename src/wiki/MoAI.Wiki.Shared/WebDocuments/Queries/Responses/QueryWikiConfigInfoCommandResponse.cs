// <copyright file="QueryWikiConfigInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Queries.Responses;

public class QueryWikiConfigInfoCommandResponse : AuditsInfo
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int WikiConfigId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Address { get; init; } = default!;

    /// <summary>
    /// 限制自动爬取的网页都在该路径之下,limit_address跟address必须具有相同域名.
    /// </summary>
    public string LimitAddress { get; init; } = default!;

    /// <summary>
    /// 最大抓取数量.
    /// </summary>
    public int LimitMaxCount { get; init; }

    /// <summary>
    /// 是否抓取其它页面.
    /// </summary>
    public bool IsCrawlOther { get; init; }

    /// <summary>
    /// 是否自动向量化.
    /// </summary>
    public bool IsAutoEmbedding { get; init; }

    /// <summary>
    /// 等待js加载完成.
    /// </summary>
    public bool IsWaitJs { get; init; }

    /// <summary>
    /// html 筛选器.
    /// </summary>
    public string Selector { get; init; }
}