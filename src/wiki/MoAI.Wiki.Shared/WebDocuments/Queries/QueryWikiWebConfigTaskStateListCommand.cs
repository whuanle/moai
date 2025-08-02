// <copyright file="QueryWikiWebConfigTaskStateListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.WebDocuments.Queries;

public class QueryWikiWebConfigTaskStateListCommand : IRequest<QueryWikiWebConfigTaskStateListCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int WikiWebConfigId { get; init; }
}

public class QueryWikiWebConfigTaskStateListCommandResponse
{
    public IReadOnlyCollection<WikiWebConfigCrawleStateItem> PageStates { get; init; } = Array.Empty<WikiWebConfigCrawleStateItem>();
    public IReadOnlyCollection<WikiConfigCrawleTaskItem> Tasks { get; init; } = Array.Empty<WikiConfigCrawleTaskItem>();
}

public class WikiWebConfigCrawleStateItem
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 爬虫id.
    /// </summary>
    public int WikiWebConfigId { get; set; }

    /// <summary>
    /// 正在爬取的地址.
    /// </summary>
    public string Url { get; set; } = default!;

    /// <summary>
    /// 信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 爬取状态.
    /// </summary>
    public CrawleState State { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}

public class WikiConfigCrawleTaskItem
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// web配置id.
    /// </summary>
    public int WikiWebConfigId { get; set; }

    /// <summary>
    /// 爬取状态.
    /// </summary>
    public CrawleState CrawleState { get; set; }

    /// <summary>
    /// 任务执行信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 选择器.
    /// </summary>
    public string Selector { get; set; } = default!;

    /// <summary>
    /// 爬取成功的页面数量.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 爬取失败的页面数量.
    /// </summary>
    public int FaildPageCount { get; set; }

    /// <summary>
    /// 每段最大token数量.
    /// </summary>
    public int MaxTokensPerParagraph { get; set; }

    /// <summary>
    /// 重叠的token数量.
    /// </summary>
    public int OverlappingTokens { get; set; }

    /// <summary>
    /// 分词器.
    /// </summary>
    public string Tokenizer { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}