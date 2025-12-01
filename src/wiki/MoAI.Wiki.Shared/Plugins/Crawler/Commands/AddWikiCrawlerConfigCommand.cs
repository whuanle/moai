using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.WikiCrawler.Commands;

/// <summary>
/// 添加一个网页爬取配置.
/// </summary>
public class AddWikiCrawlerConfigCommand : WikiCrawlerConfig, IRequest<SimpleInt>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; set; } = default!;
}