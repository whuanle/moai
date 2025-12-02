using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using MoAI.Wiki.Plugins.Crawler.Queries.Responses;

namespace MoAI.Wiki.Crawler.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCrawlerPageTasksCommand"/>
/// </summary>
public class QueryWikiCrawlerPageListCommandHandler : IRequestHandler<QueryWikiCrawlerPageTasksCommand, QueryWikiCrawlerPageTasksCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerPageListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiCrawlerPageListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCrawlerPageTasksCommandResponse> Handle(QueryWikiCrawlerPageTasksCommand request, CancellationToken cancellationToken)
    {
        var workerState = await _databaseContext.WikiPluginConfigs.Where(x => x.Id == request.ConfigId)
            .Join(_databaseContext.WorkerTasks.Where(x => x.BindType == "crawler"), a => a.Id, b => b.BindId, (a, b) => new
            {
                Id = a.Id,
                CreateTime = a.CreateTime,
                HaveTask = b != null,
                State = (WorkerState)b.State,
                Message = b.Message,
                ConfigId = request.ConfigId,
                WikiId = request.WikiId,
                Config = a.Config,
                TaskId = b.Id
            })
            .FirstOrDefaultAsync();

        if (workerState == null)
        {
            throw new BusinessException("未找到爬虫配置");
        }

        var pluginConfig = workerState.Config.JsonToObject<WikiCrawlerConfig>()!;

        var pages = await _databaseContext.WikiPluginDocumentStates
            .OrderByDescending(x => x.CreateTime)
            .Where(x => x.ConfigId == request.ConfigId)
            .Join(
            _databaseContext.WikiDocuments.Join(_databaseContext.Files, a => a.FileId, b => b.Id, (a, b) => new
            {
                Id = a.Id,
                IsEmbedding = a.IsEmbedding,
                FileName = a.FileName,
                FileSize = b.FileSize
            }),
            a => a.WikiDocumentId,
            b => b.Id,
            (a, b) => new WikiCrawlerPageItem
            {
                Id = a.Id,
                CreateTime = a.CreateTime,
                Url = a.RelevanceKey,
                IsEmbedding = b.IsEmbedding,
                Message = a.Message,
                WikiDocumentId = a.WikiDocumentId,
                CrawleState = (WorkerState)a.State,
                FileName = b.FileName,
                FileSize = b.FileSize,
                CreateUserId = a.CreateUserId
            }).ToArrayAsync();

        var task = new WikiCrawlerTask
        {
            TaskId = workerState.TaskId,
            State = workerState.State,
            Message = workerState.Message,
            ConfigId = workerState.ConfigId,
            WikiId = workerState.WikiId,
            CreateTime = workerState.CreateTime,
            Address = pluginConfig.Address,
            Selector = pluginConfig.Selector,
            FaildPageCount = pages.Count(x => x.CrawleState == WorkerState.Failed),
            PageCount = pages.Length,
        };

        return new QueryWikiCrawlerPageTasksCommandResponse
        {
            Task = task,
            Pages = pages.ToList()
        };
    }
}
