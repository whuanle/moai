using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.WikiCrawler.Queries;

namespace MoAI.Wiki.Plugins.Cawler.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCrawlerConfigListCommand"/>
/// </summary>
public class QueryWikiCrawlerConfigListCommandHandler : IRequestHandler<QueryWikiCrawlerConfigListCommand, QueryWikiCrawlerConfigListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerConfigListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiCrawlerConfigListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCrawlerConfigListCommandResponse> Handle(QueryWikiCrawlerConfigListCommand request, CancellationToken cancellationToken)
    {
        var configs = await _databaseContext.WikiPluginConfigs
            .Where(x => x.WikiId == request.WikiId && x.PluginType == "wiki_crawler")
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
                Config = x.Config
            })
            .ToListAsync(cancellationToken);

        var items = configs.Select(x => new WikiWebConfigSimpleItem
        {
            Id = x.Id,
            Title = x.Title,
            WikiId = x.WikiId,
            CreateTime = x.CreateTime,
            CreateUserId = x.CreateUserId,
            UpdateUserId = x.UpdateUserId,
            UpdateTime = x.UpdateTime,
            Address = x.Config.JsonToObject<WikiCrawlerConfig>()!.Address
        }).ToList();

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = items
        });

        return new QueryWikiCrawlerConfigListCommandResponse
        {
            Items = items
        };
    }
}
