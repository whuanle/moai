using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Feishu.Queries;
using System.Collections.Generic;

namespace MoAI.Wiki.Feishu.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiFeishuPageTasksCommand"/>
/// </summary>
public class QueryWikiFeishuPageTasksCommandHandler : IRequestHandler<QueryWikiFeishuPageTasksCommand, QueryWikiFeishuPageTasksCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiFeishuPageTasksCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryWikiFeishuPageTasksCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiFeishuPageTasksCommandResponse> Handle(QueryWikiFeishuPageTasksCommand request, CancellationToken cancellationToken)
    {
        var wikiPluginConfigEntity = await _databaseContext.WikiPluginConfigs.FirstOrDefaultAsync(x => x.Id == request.ConfigId && x.WikiId == request.WikiId, cancellationToken);

        if (wikiPluginConfigEntity == null)
        {
            throw new BusinessException("未找到知识库配置");
        }

        List<WikiFeishuPageItem> wikiCrawlerPages = new();
        if (wikiPluginConfigEntity.WorkState <= (int)WorkerState.Cancal)
        {
            // 所有页面
            var urlStates = await _databaseContext.WikiPluginConfigDocumentStates.AsNoTracking()
                .Where(x => x.ConfigId == request.ConfigId)
                .OrderByDescending(x => x.UpdateTime)
                .Select(a => new WikiFeishuPageItem
                {
                    PageId = a.Id,
                    UpdateTime = a.UpdateTime,
                    NodeToken = a.RelevanceKey,
                    ObjToken = a.RelevanceValue,
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
                .Where(x => x.ConfigId == request.ConfigId && x.State != (int)WorkerState.Successful)
                .OrderByDescending(x => x.UpdateTime)
                .Select(a => new WikiFeishuPageItem
                {
                    PageId = a.Id,
                    UpdateTime = a.UpdateTime,
                    NodeToken = a.RelevanceKey,
                    ObjToken = a.RelevanceValue,
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
                    (x, df) => new WikiFeishuPageItem
                    {
                        PageId = x.Config.Id,
                        UpdateTime = x.Config.UpdateTime,
                        NodeToken = x.Config.RelevanceKey,
                        ObjToken = x.Config.RelevanceValue,
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

        return new QueryWikiFeishuPageTasksCommandResponse
        {
            Items = wikiCrawlerPages
        };
    }
}
