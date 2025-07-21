using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// wiki网页抓取.
/// </summary>
public partial class WikiWebConfigEntity : IFullAudited
{
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Titie { get; set; }

    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// 是否抓取其它页面.
    /// </summary>
    public bool IsCrawlOther { get; set; }

    /// <summary>
    /// 抓取方式.
    /// </summary>
    public int CrawlSchame { get; set; }

    /// <summary>
    /// 限制自动爬取的网页都在该路径之下.
    /// </summary>
    public string LimitPath { get; set; } = default!;

    /// <summary>
    /// 是否自动向量化.
    /// </summary>
    public bool IsAutoEmbedding { get; set; }

    /// <summary>
    /// 抓取的页面在以前抓取过时怎么处理.
    /// </summary>
    public int SamePathRule { get; set; }

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
