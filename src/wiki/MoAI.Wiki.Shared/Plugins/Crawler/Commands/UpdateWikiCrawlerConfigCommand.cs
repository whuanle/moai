using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.WikiCrawler.Commands;

/// <summary>
/// 修改一个网页爬取配置.
/// </summary>
public class UpdateWikiCrawlerConfigCommand : WikiCrawlerConfig, IRequest<EmptyCommandResponse>
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
    /// 标题.
    /// </summary>
    public string Title { get; set; } = default!;
}
