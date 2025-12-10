using Maomi;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki;

/// <summary>
/// WikiCodeModule.
/// </summary>
[InjectModule<WikiSharedModule>]
[InjectModule<WikiApiModule>]
public class WikiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddTransient<IRequestHandler<AddWikiPluginConfigCommand<WikiCrawlerConfig>, SimpleInt>, AddWikiPluginConfigCommandHandler<WikiCrawlerConfig>>();
        context.Services.AddTransient<IRequestHandler<AddWikiPluginConfigCommand<WikiFeishuConfig>, SimpleInt>, AddWikiPluginConfigCommandHandler<WikiFeishuConfig>>();
    }
}
