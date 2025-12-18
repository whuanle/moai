using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;

namespace MoAI.Wiki.Cawler.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCrawlerPageTasksCommand"/>
/// </summary>
public class QueryWikiCrawlerPageTasksCommandHandler : IRequestHandler<QueryWikiCrawlerPageTasksCommand, QueryWikiCrawlerPageTasksCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCrawlerPageTasksCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiCrawlerPageTasksCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCrawlerPageTasksCommandResponse> Handle(QueryWikiCrawlerPageTasksCommand request, CancellationToken cancellationToken)
    {
        var wikiPluginConfigEntity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (wikiPluginConfigEntity == null)
        {
            throw new BusinessException("未找到知识库配置");
        }

        // 所有页面
        var urlStates = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
            .Where(x => x.ConfigId == request.ConfigId)
            .ToArrayAsync();

        // 已经成功爬取的
        var pageItems = await _databaseContext.WikiPluginConfigDocuments
            .OrderByDescending(x => x.UpdateTime)
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
                PageId = a.Id,
                UpdateTime = a.UpdateTime,
                Url = a.RelevanceValue,
                IsEmbedding = b.IsEmbedding,
                Message = string.Empty,
                WikiDocumentId = a.WikiDocumentId,
                FileName = b.FileName,
                FileSize = b.FileSize,
                CreateUserId = a.CreateUserId,
                State = WorkerState.Successful
            }).ToListAsync();

        foreach (var item in urlStates.OrderByDescending(x => x.UpdateTime))
        {
            if (pageItems.Any(x => x.Url == item.RelevanceValue))
            {
                continue;
            }

            pageItems.Add(new WikiCrawlerPageItem
            {
                PageId = 0,
                WikiDocumentId = 0,
                Url = item.RelevanceValue,
                State = (WorkerState)item.State,
                Message = item.Message,
                CreateUserId = item.CreateUserId,
                FileName = string.Empty,
                FileSize = 0,
                IsEmbedding = false,
                UpdateTime = item.UpdateTime
            });
        }

        pageItems = pageItems.OrderBy(x => x.State).ToList();

        return new QueryWikiCrawlerPageTasksCommandResponse
        {
            Items = pageItems
        };
    }
}
