using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// 爬虫配置.
/// </summary>
public class WikiCrawlerConfig : IWikiPluginKey
{
    /// <summary>
    /// 页面地址.
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// 限制自动爬取的网页都在该路径之下,limit_address跟address必须具有相同域名.
    /// </summary>
    public string? LimitAddress { get; set; }

    /// <summary>
    /// 伪装标识.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>
    /// 超时时间，秒.
    /// </summary>
    public int TimeOutSecond { get; init; } = 10;

    /// <summary>
    /// 最大抓取数量.
    /// </summary>
    public int LimitMaxCount { get; set; }

    /// <summary>
    /// 是否抓取其它页面.
    /// </summary>
    public bool IsCrawlOther { get; set; }

    /// <summary>
    /// 是否自动向量化.
    /// </summary>
    public bool IsAutoEmbedding { get; set; }

    /// <summary>
    /// 选择器，筛选页面的表达式，例如 ".content".
    /// </summary>
    public string Selector { get; set; } = default!;

    /// <inheritdoc/>
    public string PluginKey => "crawler";
}
