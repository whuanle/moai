using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Crawler.Models;

/// <summary>
/// Plugin 配置.
/// </summary>
public class WikiCrawlerPluginConfigSimpleItem : AuditsInfo
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 爬虫标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 页面数量.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 正在爬取.
    /// </summary>
    public bool IsWorking { get; init; }

    /// <summary>
    /// 配置.
    /// </summary>
    public WikiCrawlerConfig Config { get; init; }
}