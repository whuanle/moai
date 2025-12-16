using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Template.Models;

namespace MoAI.Wiki.Plugins.Crawler.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCrawlerPluginConfigListCommand"/>
/// </summary>
public class QueryWikiCrawlerPluginConfigListCommandHandler : IRequestHandler<QueryWikiCrawlerPluginConfigListCommand, QueryWikiCrawlerPluginConfigListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerPluginConfigListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiCrawlerPluginConfigListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCrawlerPluginConfigListCommandResponse> Handle(QueryWikiCrawlerPluginConfigListCommand request, CancellationToken cancellationToken)
    {
        var configs = await _databaseContext.WikiPluginConfigs
            .Where(x => x.WikiId == request.WikiId && x.PluginType == "crawler")
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                Count = _databaseContext.WikiPluginDocumentStates.Where(a => a.ConfigId == x.Id).Count(),
                IsWorking = _databaseContext.WorkerTasks.Where(a => a.BindType == "crawler" && a.BindId == x.Id && a.State <= (int)WorkerState.Processing).Any(),
                Config = x.Config,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        var items = configs.Select(x => new WikiCrawlerPluginConfigSimpleItem
        {
            ConfigId = x.Id,
            Title = x.Title,
            WikiId = x.WikiId,
            Config = x.Config.JsonToObject<WikiCrawlerConfig>()!,
            IsWorking = x.IsWorking,
            Count = x.Count,
            CreateTime = x.CreateTime,
            CreateUserId = x.CreateUserId,
            UpdateUserId = x.UpdateUserId,
            UpdateTime = x.UpdateTime
        }).ToList();

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = items
        });

        return new QueryWikiCrawlerPluginConfigListCommandResponse
        {
            Items = items
        };
    }
}
