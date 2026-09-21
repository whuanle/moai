using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Models;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiSourcesCommand"/>
/// </summary>
public class QueryWikiSourcesCommandHandler : IRequestHandler<QueryWikiSourcesCommand, QueryWikiSourcesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;
    private readonly IFeishuConnectionStatus _feishuConnectionStatus;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiSourcesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    /// <param name="feishuConnectionStatus">飞书长连接在线状态查询.</param>
    public QueryWikiSourcesCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserInfoFillService userInfoFillService,
        IFeishuConnectionStatus feishuConnectionStatus)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
        _feishuConnectionStatus = feishuConnectionStatus;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiSourcesCommandResponse> Handle(QueryWikiSourcesCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var sources = await _databaseContext.WikiSources
            .Where(x => x.WikiId == wiki.Id)
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        var sourceIds = sources.Select(x => x.Id).ToArray();
        var documentCounts = await _databaseContext.WikiSourceDocuments
            .Where(x => sourceIds.Contains(x.SourceId))
            .GroupBy(x => x.SourceId)
            .Select(g => new { SourceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SourceId, x => x.Count, cancellationToken);

        var feishuAppIds = sources
            .Select(x => WikiSourceConfigJson.DeserializeFeishu(x.Config)?.FeishuAppId)
            .Where(x => x.HasValue && x.Value != Guid.Empty)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var feishuApps = new Dictionary<Guid, (string Name, string AppId)>();
        if (feishuAppIds.Length > 0)
        {
            var apps = await _databaseContext.FeishuApps
                .Where(x => feishuAppIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name, x.AppId })
                .ToListAsync(cancellationToken);

            foreach (var app in apps)
            {
                feishuApps[app.Id] = (app.Name, app.AppId);
            }
        }

        var items = new List<WikiSourceItem>();
        foreach (var source in sources)
        {
            var isCrawler = source.SourceType == (int)WikiSourceType.Crawler;
            var config = isCrawler ? null : WikiSourceConfigJson.DeserializeFeishu(source.Config);
            var crawlerConfig = isCrawler ? WikiSourceConfigJson.DeserializeCrawler(source.Config) : null;
            var appId = config?.FeishuAppId ?? Guid.Empty;
            feishuApps.TryGetValue(appId, out var found);
            var app = appId != Guid.Empty && found.AppId != null ? found : ((string Name, string AppId)?)null;

            items.Add(new WikiSourceItem
            {
                SourceId = source.Id,
                WikiId = source.WikiId,
                SourceType = (WikiSourceType)source.SourceType,
                Name = source.Name,
                Description = source.Description ?? string.Empty,
                IsEnable = source.IsEnable,
                Cron = source.Cron ?? string.Empty,
                IsEventSubscription = source.IsEventSubscription,
                WorkflowConfig = WikiWorkflowConfigJson.Deserialize(source.WorkflowConfig),
                Feishu = config,
                Crawler = crawlerConfig,
                FeishuAppName = app?.Name,
                FeishuAppOpenId = app?.AppId,
                FeishuAppOnline = appId != Guid.Empty && _feishuConnectionStatus.IsOnline(appId),
                LastSyncStatus = (WikiSourceSyncStatus)source.LastSyncStatus,
                LastSyncTime = source.LastSyncTime,
                LastSyncMessage = source.LastSyncMessage ?? string.Empty,
                DocumentCount = documentCounts.TryGetValue(source.Id, out var count) ? count : 0,
                CreateUserId = (int)source.CreateUserId,
                CreateTime = source.CreateTime,
                UpdateUserId = (int)source.UpdateUserId,
                UpdateTime = source.UpdateTime,
            });
        }

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryWikiSourcesCommandResponse
        {
            MyRole = (int)myRole.Value,
            Items = items,
        };
    }
}
