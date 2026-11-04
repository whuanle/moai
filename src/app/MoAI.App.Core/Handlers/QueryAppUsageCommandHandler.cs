using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppUsageCommand"/>
/// </summary>
public class QueryAppUsageCommandHandler : IRequestHandler<QueryAppUsageCommand, QueryAppUsageCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly AppUsageAggregateCache _usageAggregateCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppUsageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="usageAggregateCache">应用用量聚合缓存.</param>
    public QueryAppUsageCommandHandler(DatabaseContext databaseContext, ITeamService teamService, AppUsageAggregateCache usageAggregateCache)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _usageAggregateCache = usageAggregateCache;
    }

    /// <inheritdoc/>
    public async Task<QueryAppUsageCommandResponse> Handle(QueryAppUsageCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId, x.UseCount, x.MessageCount })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能查看监控.") { StatusCode = 403 };
        }

        // tokens/调用次数/按模型分布：聚合用量表且分钟级滞后，走 1 分钟 Redis 缓存避免每次刷新都查库
        var aggregate = await _usageAggregateCache.GetAsync(app.Id, cancellationToken).ConfigureAwait(false);
        if (aggregate == null)
        {
            aggregate = await QueryAggregateAsync(app.TeamId, app.Id, cancellationToken).ConfigureAwait(false);
            await _usageAggregateCache.SetAsync(app.Id, aggregate, cancellationToken).ConfigureAwait(false);
        }

        // 使用活动统计：会话数 / 去重用户数 / 最近使用时间为实时小聚合（与缓存解耦，删除会话立即生效）；
        // 去重用户用 SELECT DISTINCT（GroupBy(1)+Distinct().Count() 会翻译出非法 COUNT(DISTINCT)，PostgreSQL 42601）
        var sessionQuery = _databaseContext.AppAgentSessions.AsNoTracking()
            .Where(x => x.AppId == app.Id);

        var sessionCount = await sessionQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var lastActiveTime = await sessionQuery
            .MaxAsync(x => (DateTimeOffset?)x.LastMessageTime, cancellationToken).ConfigureAwait(false);

        var userKeys = await _databaseContext.AppAgentSessions.AsNoTracking()
            .Where(x => x.AppId == app.Id)
            .Select(x => new { x.CreateUserId, x.UserType })
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new QueryAppUsageCommandResponse
        {
            Summary = aggregate.Summary,
            ByModel = aggregate.ByModel,
            Stats = new AppUsageActivityStats
            {
                UseCount = app.UseCount,
                SessionCount = sessionCount,
                UserCount = userKeys.Count,
                MessageCount = app.MessageCount,
                LastActiveTime = lastActiveTime,
            },
        };
    }

    /// <summary>
    /// 查询聚合用量表的汇总与按模型分布.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>聚合结果.</returns>
    private async Task<AppUsageAggregate> QueryAggregateAsync(int teamId, Guid appId, CancellationToken cancellationToken)
    {
        var byModel = await _databaseContext.AiModelTokenAudits.AsNoTracking()
            .Where(x => x.TeamId == teamId
                && x.UseType == (int)AiModelUseType.App
                && x.UseResourceId == appId)
            .GroupBy(x => x.ModelId)
            .Select(g => new
            {
                ModelId = g.Key,
                CallCount = g.Sum(x => (long)x.Count),
                PromptTokens = g.Sum(x => (long)x.PromptTokens),
                CompletionTokens = g.Sum(x => (long)x.CompletionTokens),
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
                CachedTokens = g.Sum(x => (long)x.CachedTokens),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var modelIds = byModel.Select(x => x.ModelId).ToList();
        var names = await _databaseContext.AiModels.AsNoTracking()
            .Where(m => modelIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken).ConfigureAwait(false);

        var byModelItems = byModel
            .OrderByDescending(x => x.TotalTokens)
            .Select(x => new AppUsageModelItem
            {
                ModelId = x.ModelId,
                ModelName = names.TryGetValue(x.ModelId, out var n) ? n : x.ModelId.ToString(),
                CallCount = x.CallCount,
                PromptTokens = x.PromptTokens,
                CompletionTokens = x.CompletionTokens,
                TotalTokens = x.TotalTokens,
                CachedTokens = x.CachedTokens,
            })
            .ToList();

        var summary = new AppUsageSummary
        {
            CallCount = byModelItems.Sum(x => x.CallCount),
            PromptTokens = byModelItems.Sum(x => x.PromptTokens),
            CompletionTokens = byModelItems.Sum(x => x.CompletionTokens),
            TotalTokens = byModelItems.Sum(x => x.TotalTokens),
            CachedTokens = byModelItems.Sum(x => x.CachedTokens),
        };

        return new AppUsageAggregate(summary, byModelItems);
    }
}
