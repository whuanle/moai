using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.User.Queries;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.WikiCrawler.Queries;
using MoAI.Wiki.Plugins.WikiCrawler.Queries.Responses;

namespace MoAI.Wiki.Plugins.Cawler.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCrawlerConfigICommand"/>
/// </summary>
public class QueryWikiConfigInfoCommandHandler : IRequestHandler<QueryWikiCrawlerConfigICommand, QueryWikiCrawlerConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiConfigInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiConfigInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCrawlerConfigCommandResponse> Handle(QueryWikiCrawlerConfigICommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.WikiPluginConfigs
            .Where(x => x.Id == request.WikiCrawlerConfigId && x.WikiId == request.WikiId)
            .Select(x => new
            {
                ConfigId = x.Id,
                Title = x.Title,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
                Config = x.Config
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            throw new BusinessException("未找到知识库爬虫配置");
        }

        var config = result.Config.JsonToObject<WikiCrawlerConfig>()!;
        var responseConfig = new QueryWikiCrawlerConfigCommandResponse
        {
            ConfigId = result.ConfigId,
            Title = result.Title,
            WikiId = result.WikiId,
            CreateTime = result.CreateTime,
            CreateUserId = result.CreateUserId,
            UpdateUserId = result.UpdateUserId,
            UpdateTime = result.UpdateTime,
            Address = config.Address.ToString(),
            LimitAddress = config.LimitAddress?.ToString() ?? string.Empty,
            LimitMaxCount = config.LimitMaxCount,
            IsCrawlOther = config.IsCrawlOther,
            IsAutoEmbedding = config.IsAutoEmbedding,
            Selector = config.Selector,
        };

        await _mediator.Send(new FillUserInfoCommand { Items = new[] { responseConfig } });

        return responseConfig;
    }
}
