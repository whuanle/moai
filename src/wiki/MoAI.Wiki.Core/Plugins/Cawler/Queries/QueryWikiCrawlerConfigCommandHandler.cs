using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using MoAI.Wiki.Plugins.Template.Models;

namespace MoAI.Wiki.Plugins.Cawler.Queries;

/// <summary>
/// <see cref="QueryWikiCrawlerConfigCommand"/>
/// </summary>
public class QueryWikiCrawlerConfigCommandHandler : IRequestHandler<QueryWikiCrawlerConfigCommand, QueryWikiCrawlerConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiCrawlerConfigCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCrawlerConfigCommandResponse> Handle(QueryWikiCrawlerConfigCommand request, CancellationToken cancellationToken)
    {
        var wikiPluginConfigEntity = await _databaseContext.WikiPluginConfigs
            .FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId && x.PluginType == "crawler", cancellationToken);

        if (wikiPluginConfigEntity == null)
        {
            throw new BusinessException("未找到知识库配置");
        }

        var config = wikiPluginConfigEntity.Config.JsonToObject<WikiCrawlerConfig>()!;
        var responseConfig = new QueryWikiCrawlerConfigCommandResponse
        {
            ConfigId = wikiPluginConfigEntity.Id,
            Title = wikiPluginConfigEntity.Title,
            WikiId = wikiPluginConfigEntity.WikiId,
            CreateTime = wikiPluginConfigEntity.CreateTime,
            CreateUserId = wikiPluginConfigEntity.CreateUserId,
            UpdateUserId = wikiPluginConfigEntity.UpdateUserId,
            UpdateTime = wikiPluginConfigEntity.UpdateTime,
            Config = config,
            WorkMessage = wikiPluginConfigEntity.WorkMessage,
            WorkState = (WorkerState)wikiPluginConfigEntity.WorkState,
        };

        await _mediator.Send(new FillUserInfoCommand { Items = new[] { responseConfig } });

        return responseConfig;
    }
}
