using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// web爬虫状态.
/// </summary>
public partial class WikiWebCrawleTaskEntity : IFullAudited
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
    public int CrawleState { get; set; }

    /// <summary>
    /// 任务执行信息.
    /// </summary>
    public string Message { get; set; } = default!;

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

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
