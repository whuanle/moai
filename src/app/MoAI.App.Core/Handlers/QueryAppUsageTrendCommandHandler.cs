using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Statistics.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppUsageTrendCommand"/>
/// </summary>
public class QueryAppUsageTrendCommandHandler : IRequestHandler<QueryAppUsageTrendCommand, QueryAppUsageTrendCommandResponse>
{
    /// <summary>
    /// 按模型 token 序列封顶条数（按区间合计 token 降序取前 N，避免多模型应用线条过多不可读）.
    /// </summary>
    public const int MaxModelSeries = 8;

    private const string ModelUsageName = ResourceUsageNames.AiModel;

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly AppUsageTrendCache _trendCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppUsageTrendCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="trendCache">应用使用趋势缓存.</param>
    public QueryAppUsageTrendCommandHandler(DatabaseContext databaseContext, ITeamService teamService, AppUsageTrendCache trendCache)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _trendCache = trendCache;
    }

    /// <inheritdoc/>
    public async Task<QueryAppUsageTrendCommandResponse> Handle(QueryAppUsageTrendCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId })
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

        // 趋势含按模型 token 日聚合（LIKE 后缀聚合查询），与用量聚合同样分钟级滞后，走 1 分钟缓存
        var cached = await _trendCache.GetAsync(app.Id, request.Days, cancellationToken).ConfigureAwait(false);
        if (cached != null)
        {
            return cached;
        }

        var response = await QueryTrendAsync(app.Id, request.Days, cancellationToken).ConfigureAwait(false);
        await _trendCache.SetAsync(app.Id, request.Days, response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// 查询使用趋势：使用次数/消息条数按日透视 + 各模型 token 按日聚合序列.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="days">趋势区间天数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>趋势响应.</returns>
    private async Task<QueryAppUsageTrendCommandResponse> QueryTrendAsync(Guid appId, int days, CancellationToken cancellationToken)
    {
        var endDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var startDate = endDate.AddDays(-(days - 1));
        var appIdText = appId.ToString("N");

        // 统一日表双指标（使用次数/消息条数）按日透视；按小时粒度落库的行在内存并入当天
        var names = new[]
        {
            ResourceUsageNames.App,
            ResourceUsageNames.AppMessage,
        };

        var rows = await _databaseContext.ResourceUsageDailies.AsNoTracking()
            .Where(x => x.UsageDate >= startDate
                && x.UsageDate <= endDate
                && x.ResourceId == appIdText
                && names.Contains(x.Name))
            .Select(x => new { x.UsageDate, x.Name, x.Count })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var daily = rows
            .GroupBy(x => x.UsageDate)
            .ToDictionary(
                g => g.Key,
                g => new AppUsageTrendItem
                {
                    Date = g.Key,
                    UseCount = g.Where(x => x.Name == ResourceUsageNames.App).Sum(x => x.Count),
                    MessageCount = g.Where(x => x.Name == ResourceUsageNames.AppMessage).Sum(x => x.Count),
                });

        // 区间内无数据的日期补 0，趋势图连续
        var items = new List<AppUsageTrendItem>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            items.Add(daily.TryGetValue(date, out var item)
                ? item
                : new AppUsageTrendItem { Date = date });
        }

        var modelTokenSeries = await QueryModelTokenSeriesAsync(appIdText, startDate, endDate, cancellationToken).ConfigureAwait(false);

        return new QueryAppUsageTrendCommandResponse
        {
            Items = items,
            ModelTokenSeries = modelTokenSeries,
            StartDate = startDate,
            EndDate = endDate,
        };
    }

    /// <summary>
    /// 查询各模型 token 按日序列：统一日表 ai-model-usage 行的 resource_id 为复合 key
    /// （v1:{modelId}:{teamId}:{userId}:{useType}:{useResourceId}:{metric}），按
    /// 「resource_id 以 :{appIdN}:total 结尾」后缀匹配该应用全部用户的模型总 token 行，
    /// SQL 端按 日期+模型段 聚合（返回行数 = 天数×模型数，规避 hour 粒度行放大），
    /// 内存按区间合计取 top {MaxModelSeries} 并零填充完整日期区间.
    /// </summary>
    /// <param name="appIdText">应用 id 文本（N 格式）.</param>
    /// <param name="startDate">起始日期.</param>
    /// <param name="endDate">结束日期.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>各模型 token 序列.</returns>
    private async Task<List<AppUsageModelTokenSeries>> QueryModelTokenSeriesAsync(string appIdText, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var likePattern = $"%:{appIdText}:total";
        var rows = await _databaseContext.Database
            .SqlQuery<ModelTokenRow>($"""
                SELECT usage_date AS "Date",
                       split_part(resource_id, ':', 2)::uuid AS "ModelId",
                       SUM(count) AS "Tokens"
                FROM resource_usage_daily
                WHERE name = {ModelUsageName}
                  AND usage_date >= {startDate}
                  AND usage_date <= {endDate}
                  AND resource_id LIKE {likePattern}
                GROUP BY 1, 2
                """)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return new List<AppUsageModelTokenSeries>();
        }

        // 按区间合计 token 降序取前 N 条序列
        var byModel = rows
            .GroupBy(x => x.ModelId)
            .OrderByDescending(g => g.Sum(x => x.Tokens))
            .Take(MaxModelSeries)
            .ToList();

        var modelIds = byModel.Select(g => g.Key).ToList();
        var names = await _databaseContext.AiModels.AsNoTracking()
            .Where(m => modelIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken).ConfigureAwait(false);

        var series = new List<AppUsageModelTokenSeries>();
        foreach (var group in byModel)
        {
            var daily = group.ToDictionary(x => x.Date, x => x.Tokens);
            var points = new List<AppUsageModelTokenPoint>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                points.Add(new AppUsageModelTokenPoint
                {
                    Date = date,
                    Tokens = daily.TryGetValue(date, out var tokens) ? tokens : 0,
                });
            }

            series.Add(new AppUsageModelTokenSeries
            {
                ModelId = group.Key,
                ModelName = names.TryGetValue(group.Key, out var n) ? n : group.Key.ToString(),
                Points = points,
            });
        }

        return series;
    }

    /// <summary>
    /// 按模型 token 日聚合行（原生 SQL 投影，别名与属性名一致）.
    /// </summary>
    private sealed class ModelTokenRow
    {
        /// <summary>统计日期.</summary>
        public DateOnly Date { get; set; }

        /// <summary>模型 id.</summary>
        public Guid ModelId { get; set; }

        /// <summary>当日合计 token.</summary>
        public long Tokens { get; set; }
    }
}
