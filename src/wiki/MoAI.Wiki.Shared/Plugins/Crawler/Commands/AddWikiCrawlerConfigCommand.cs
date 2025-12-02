using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Crawler.Commands;

/// <summary>
/// 添加一个网页爬取配置.
/// </summary>
public class AddWikiCrawlerConfigCommand : AddWikiPluginConfigCommand<WikiCrawlerConfig>, IRequest<SimpleInt>
{
}