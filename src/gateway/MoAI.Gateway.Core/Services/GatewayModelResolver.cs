using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;

namespace MoAI.Gateway.Services;

/// <summary>
/// 网关模型解析：把密钥所属团队 + 请求中的模型名解析为已授权的模型与渠道.
/// </summary>
public class GatewayModelResolver
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayModelResolver"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public GatewayModelResolver(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 解析团队可用的单个模型，要求模型与渠道均启用且（公开模型 或 已授权给该团队）.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="modelId">网关请求中的模型名（即 ai_model.model_id）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回模型与渠道，未授权或不可用时为 null.</returns>
    public async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveAsync(int teamId, string modelId, CancellationToken cancellationToken = default)
    {
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.ModelId == modelId && m.Enabled && c.Enabled
                    orderby m.Id
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var first = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (first == null)
        {
            var modelIds = candidates.Select(x => x.m.Id).ToList();
            var authorizedModelId = await _databaseContext.AiModelAuthorizations
                .Where(x => x.TeamId == teamId && modelIds.Contains(x.AiModelId))
                .Select(x => (Guid?)x.AiModelId)
                .FirstOrDefaultAsync(cancellationToken);
            if (authorizedModelId != null)
            {
                first = candidates.FirstOrDefault(x => x.m.Id == authorizedModelId.Value);
            }
        }

        return first == null ? null : (first.m, first.c);
    }

    /// <summary>
    /// 查询团队全部可用模型（公开模型 + 已授权模型，含渠道、额度与开放接口累计用量），供 /v1/models 与团队管理页使用.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回模型视图集合.</returns>
    public async Task<IReadOnlyList<TeamGatewayModelView>> ListAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var authorizedModelIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == teamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Enabled && c.Enabled && (m.IsPublic || authorizedModelIds.Contains(m.Id))
                    orderby m.ModelId, m.Id
                    select new { m, c };

        var models = await query.ToListAsync(cancellationToken);
        var modelIds = models.Select(x => x.m.Id).ToList();

        // 额度规则：私有模型按团队（team_id>0），公开模型全局共享（team_id=0）.
        var limits = await _databaseContext.AiModelLimits
            .Where(x => (x.TeamId == teamId || x.TeamId == 0) && modelIds.Contains(x.ModelId))
            .ToListAsync(cancellationToken);

        var quotas = await _databaseContext.AiModelQuota
            .Where(x => (x.TeamId == teamId || x.TeamId == 0) && modelIds.Contains(x.ModelId))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.Now;
        var utcNow = DateTime.UtcNow;
        var activeLimits = limits
            .Where(x => x.ExpirationTime == null || x.ExpirationTime > utcNow)
            .ToLookup(x => x.ModelId);

        var quotaByLimitId = quotas.ToDictionary(x => x.LimitId);

        var usages = await _databaseContext.AiModelTokenAudits
            .Where(x => x.TeamId == teamId
                && x.UseType == (int)Database.Enums.AiModelUseType.OpenApi
                && modelIds.Contains(x.ModelId))
            .GroupBy(x => x.ModelId)
            .Select(g => new { ModelId = g.Key, Total = g.Sum(x => (long)x.TotalTokens) })
            .ToListAsync(cancellationToken);

        var usageByModelId = usages.ToDictionary(x => x.ModelId, x => x.Total);

        var views = new List<TeamGatewayModelView>();
        foreach (var item in models)
        {
            var limit = activeLimits[item.m.Id].OrderBy(x => x.Id).FirstOrDefault();
            TeamGatewayQuotaView? quotaView = null;
            if (limit != null && limit.LimitValue > 0)
            {
                long used = 0;
                DateTimeOffset? periodEnd = null;
                if (quotaByLimitId.TryGetValue(limit.Id, out var quota))
                {
                    if (limit.PeriodUnit != 0 && quota.PeriodEnd <= now)
                    {
                        used = 0;
                    }
                    else
                    {
                        used = quota.UsedTokens;
                        periodEnd = quota.PeriodEnd;
                    }
                }

                quotaView = new TeamGatewayQuotaView
                {
                    PeriodValue = limit.PeriodValue,
                    PeriodUnit = limit.PeriodUnit,
                    LimitValue = limit.LimitValue,
                    UsedTokens = used,
                    PeriodEnd = periodEnd,
                    ExpirationTime = limit.ExpirationTime.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(limit.ExpirationTime.Value, DateTimeKind.Utc)) : null,
                };
            }

            views.Add(new TeamGatewayModelView
            {
                Model = item.m,
                Channel = item.c,
                Quota = quotaView,
                TotalUsedTokens = usageByModelId.TryGetValue(item.m.Id, out var total) ? total : 0,
            });
        }

        return views;
    }

    /// <summary>
    /// 团队可用模型视图.
    /// </summary>
    public sealed class TeamGatewayModelView
    {
        /// <summary>
        /// 模型.
        /// </summary>
        public AiModelEntity Model { get; init; } = default!;

        /// <summary>
        /// 渠道.
        /// </summary>
        public AiChannelEntity Channel { get; init; } = default!;

        /// <summary>
        /// 额度，null=不限额.
        /// </summary>
        public TeamGatewayQuotaView? Quota { get; init; }

        /// <summary>
        /// 开放接口累计消耗 tokens.
        /// </summary>
        public long TotalUsedTokens { get; init; }
    }

    /// <summary>
    /// 额度视图.
    /// </summary>
    public sealed class TeamGatewayQuotaView
    {
        /// <summary>
        /// 周期长度.
        /// </summary>
        public int PeriodValue { get; init; }

        /// <summary>
        /// 周期单位：0=不重置 1=小时 2=天 3=周 4=月.
        /// </summary>
        public int PeriodUnit { get; init; }

        /// <summary>
        /// 每周期 tokens 上限.
        /// </summary>
        public long LimitValue { get; init; }

        /// <summary>
        /// 当前周期已消耗.
        /// </summary>
        public long UsedTokens { get; init; }

        /// <summary>
        /// 当前周期终点，不重置规则为 null.
        /// </summary>
        public DateTimeOffset? PeriodEnd { get; init; }

        /// <summary>
        /// 规则有效期，null=长期.
        /// </summary>
        public DateTimeOffset? ExpirationTime { get; init; }
    }
}
