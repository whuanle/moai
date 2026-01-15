using LinqKit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Crawler.Queries;
using Org.BouncyCastle.Asn1.Cmp;

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

        // 任务进行中只统计 WikiPluginConfigDocumentStates，任务已完成统计 WikiPluginConfigDocuments
        List<WikiCrawlerPageItem> wikiCrawlerPages = new();
        if (wikiPluginConfigEntity.WorkState <= (int)WorkerState.Cancal)
        {
            // 所有页面
            var urlStates = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
                .Where(x => x.ConfigId == request.ConfigId)
                .OrderByDescending(x => x.UpdateTime)
                .Select(a => new WikiCrawlerPageItem
                {
                    PageId = a.Id,
                    UpdateTime = a.UpdateTime,
                    Url = a.RelevanceValue,
                    Message = string.Empty,
                    CreateUserId = a.CreateUserId,
                    State = (WorkerState)a.State
                })
                .ToArrayAsync();

            wikiCrawlerPages.AddRange(urlStates);
        }
        else
        {
            // 除开成功的页面
            var urlStates = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
                .OrderByDescending(x => x.UpdateTime)
                .Where(x => x.ConfigId == request.ConfigId && x.State != (int)WorkerState.Successful)
                .Select(a => new WikiCrawlerPageItem
                {
                    PageId = a.Id,
                    UpdateTime = a.UpdateTime,
                    Url = a.RelevanceValue,
                    Message = string.Empty,
                    CreateUserId = a.CreateUserId,
                    State = (WorkerState)a.State
                })
                .ToArrayAsync();
            wikiCrawlerPages.AddRange(urlStates);

            // 已经成功爬取的
            var docWithFiles = _databaseContext.WikiDocuments.AsNoTracking()
                .Join(
                _databaseContext.Files.AsNoTracking(),
                doc => doc.FileId,
                file => file.Id,
                (doc, file) => new { Doc = doc, File = file });

            var pageItems = await _databaseContext.WikiPluginConfigDocuments.AsNoTracking()
                .Where(x => x.ConfigId == request.ConfigId)
                .OrderByDescending(x => x.UpdateTime)
                .GroupJoin(
                docWithFiles,
                config => config.WikiDocumentId,
                df => df.Doc.Id,
                (config, dfs) => new { Config = config, Dfs = dfs })
                .SelectMany(
                    x => x.Dfs.DefaultIfEmpty(),
                    (x, df) => new WikiCrawlerPageItem
                    {
                        PageId = x.Config.Id,
                        UpdateTime = x.Config.UpdateTime,
                        Url = x.Config.RelevanceValue,
                        IsEmbedding = df != null && df.Doc.IsEmbedding,
                        Message = string.Empty,
                        WikiDocumentId = x.Config.WikiDocumentId,
                        FileName = df != null ? df.Doc.FileName : string.Empty,
                        FileSize = df != null ? df.File.FileSize : 0,
                        CreateUserId = x.Config.CreateUserId,
                        State = WorkerState.Successful
                    })
                .ToListAsync();

            wikiCrawlerPages.AddRange(pageItems);
        }

        return new QueryWikiCrawlerPageTasksCommandResponse
        {
            Items = wikiCrawlerPages
        };
    }
}
