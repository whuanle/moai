using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Crawler.Queries.Responses;

/// <summary>
/// 配置.
/// </summary>
public class QueryWikiCrawlerConfigCommandResponse : AuditsInfo
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }

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
    /// html 筛选器.
    /// </summary>
    public required string Selector { get; init; }
}