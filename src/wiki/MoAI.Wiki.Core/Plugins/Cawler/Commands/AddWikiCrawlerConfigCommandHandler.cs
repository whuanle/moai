using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.WikiCrawler.Commands;

namespace MoAI.Wiki.Plugins.Cawler.Commands;

/// <summary>
/// <inheritdoc cref="AddWikiCrawlerConfigCommand"/>
/// </summary>
public class AddWikiCrawlerConfigCommandHandler : IRequestHandler<AddWikiCrawlerConfigCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWikiCrawlerConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AddWikiCrawlerConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(AddWikiCrawlerConfigCommand request, CancellationToken cancellationToken)
    {
        var existWiki = await _databaseContext.Wikis.AnyAsync(x => x.Id == request.WikiId, cancellationToken);
        if (!existWiki)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        var existConfig = await _databaseContext.WikiPluginConfigs.AnyAsync(x => x.WikiId == request.WikiId && x.Title == request.Title && x.PluginType == "wiki_crawler", cancellationToken);
        if (existConfig)
        {
            throw new BusinessException("存在相同名称的爬虫配置");
        }

        var entity = new WikiPluginConfigEntity
        {
            WikiId = request.WikiId,
            Title = request.Title,
            PluginType = "wiki_crawler",
            Config = ((WikiCrawlerConfig)request).ToJsonString()
        };

        _databaseContext.WikiPluginConfigs.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleInt { Value = entity.Id };
    }
}
