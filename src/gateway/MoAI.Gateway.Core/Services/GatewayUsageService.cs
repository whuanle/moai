using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;

namespace MoAI.Gateway.Services;

/// <summary>
/// 网关用量服务：调用前额度预检（惰性重置过期周期），调用后原子扣减并记录用量.
/// </summary>
public class GatewayUsageService
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<GatewayUsageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayUsageService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="logger">日志.</param>
    public GatewayUsageService(DatabaseContext databaseContext, ILogger<GatewayUsageService> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <summary>
    /// 调用前额度预检：任一生效额度规则耗尽即抛出 429，无规则视为不限额.
    /// </summary>
    /// <param name="modelId">模型 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public async Task CheckQuotaAsync(Guid modelId, int teamId, CancellationToken cancellationToken = default)
    {
        var limits = await ListActiveLimitsAsync(modelId, teamId, cancellationToken);
        foreach (var limit in limits)
        {
            if (limit.LimitValue <= 0)
            {
                // limit_value<=0 表示该规则不限流.
                continue;
            }

            var quota = await EnsureQuotaAsync(limit, cancellationToken);
            if (quota.UsedTokens >= quota.TotalLimit)
            {
                throw new BusinessException("模型额度已耗尽，请联系团队管理员.") { StatusCode = 429 };
            }
        }
    }

    /// <summary>
    /// 调用完成后记账：原子累加各额度规则余额、写入使用日志、累加 token 统计、刷新密钥最近使用时间.
    /// </summary>
    /// <param name="modelId">模型 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">密钥创建者用户 id.</param>
    /// <param name="apiKeyId">密钥 id.</param>
    /// <param name="channelProviderKey">渠道供应商标识.</param>
    /// <param name="promptTokens">输入 tokens.</param>
    /// <param name="completionTokens">输出 tokens.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public async Task RecordAsync(Guid modelId, int teamId, long userId, Guid apiKeyId, string channelProviderKey, int promptTokens, int completionTokens, CancellationToken cancellationToken = default)
    {
        var totalTokens = promptTokens + completionTokens;

        try
        {
            var limits = await ListActiveLimitsAsync(modelId, teamId, cancellationToken);
            foreach (var limit in limits)
            {
                if (limit.LimitValue <= 0)
                {
                    continue;
                }

                await IncrementQuotaAsync(limit, totalTokens, cancellationToken);
            }

            var now = DateTimeOffset.Now;
            _databaseContext.AiModelUsageLogs.Add(new AiModelUsageLogEntity
            {
                ModelId = modelId,
                TeamId = teamId,
                UserId = (int)userId,
                CompletionTokens = completionTokens,
                PromptTokens = promptTokens,
                TotalTokens = totalTokens,
                UseType = (int)AiModelUseType.OpenApi,
                UseResourceId = 0,
                Channel = TruncateChannel(channelProviderKey),
            });

            var audit = await _databaseContext.AiModelTokenAudits.FirstOrDefaultAsync(
                x => x.ModelId == modelId
                    && x.TeamId == teamId
                    && x.UserId == (int)userId
                    && x.UseType == (int)AiModelUseType.OpenApi
                    && x.UseResourceId == apiKeyId,
                cancellationToken);
            if (audit == null)
            {
                _databaseContext.AiModelTokenAudits.Add(new AiModelTokenAuditEntity
                {
                    ModelId = modelId,
                    TeamId = teamId,
                    UserId = (int)userId,
                    UseType = (int)AiModelUseType.OpenApi,
                    UseResourceId = apiKeyId,
                    CompletionTokens = completionTokens,
                    PromptTokens = promptTokens,
                    TotalTokens = totalTokens,
                    Count = 1,
                });
            }
            else
            {
                audit.CompletionTokens += completionTokens;
                audit.PromptTokens += promptTokens;
                audit.TotalTokens += totalTokens;
                audit.Count += 1;
            }

            await _databaseContext.TeamApiKeys
                .Where(x => x.Id == apiKeyId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastUsedTime, now), cancellationToken);

            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // 记账失败不影响本次调用的返回结果，但要留下日志便于对账.
            _logger.LogError(ex, "网关用量记账失败. ModelId={ModelId}, TeamId={TeamId}, ApiKeyId={ApiKeyId}", modelId, teamId, apiKeyId);
        }
    }

    /// <summary>
    /// 列出对本次调用生效的额度规则：私有模型按团队（team_id=teamId），公开模型全局共享（team_id=0）.
    /// </summary>
    private async Task<List<AiModelLimitEntity>> ListActiveLimitsAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        return await _databaseContext.AiModelLimits
            .Where(x => x.ModelId == modelId
                && (x.TeamId == teamId || x.TeamId == 0)
                && (x.ExpirationTime == null || x.ExpirationTime > utcNow))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取额度余额行，缺失时惰性补建（兼容先建规则后建余额的历史数据）.
    /// </summary>
    private async Task<AiModelQuotumEntity> EnsureQuotaAsync(AiModelLimitEntity limit, CancellationToken cancellationToken)
    {
        var quota = await _databaseContext.AiModelQuota.FirstOrDefaultAsync(x => x.LimitId == limit.Id, cancellationToken);
        if (quota == null)
        {
            var now = DateTimeOffset.Now;
            quota = new AiModelQuotumEntity
            {
                LimitId = limit.Id,
                ModelId = limit.ModelId,
                TeamId = limit.TeamId,
                TotalLimit = limit.LimitValue,
                UsedTokens = 0,
                PeriodStart = now,
                PeriodEnd = QuotaPeriodHelper.ComputePeriodEnd(now, limit.PeriodValue, limit.PeriodUnit),
            };
            _databaseContext.AiModelQuota.Add(quota);
            await _databaseContext.SaveChangesAsync(cancellationToken);
            return quota;
        }

        return await ResetIfExpiredAsync(quota, limit, cancellationToken);
    }

    private async Task<AiModelQuotumEntity> ResetIfExpiredAsync(AiModelQuotumEntity quota, AiModelLimitEntity limit, CancellationToken cancellationToken)
    {
        if (limit.PeriodUnit == 0 || quota.PeriodEnd > DateTimeOffset.Now)
        {
            if (quota.TotalLimit != limit.LimitValue)
            {
                quota.TotalLimit = limit.LimitValue;
                await _databaseContext.SaveChangesAsync(cancellationToken);
            }

            return quota;
        }

        var now = DateTimeOffset.Now;
        quota.UsedTokens = 0;
        quota.TotalLimit = limit.LimitValue;
        quota.PeriodStart = now;
        quota.PeriodEnd = QuotaPeriodHelper.ComputePeriodEnd(now, limit.PeriodValue, limit.PeriodUnit);
        quota.LastResetTime = DateTime.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return quota;
    }

    /// <summary>
    /// 原子累加已用 tokens：周期内用条件更新防止并发超扣；周期已过期时先重置再累加.
    /// </summary>
    private async Task IncrementQuotaAsync(AiModelLimitEntity limit, long totalTokens, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;

        // 先把过期周期的余额行重置（条件更新，避免与其它请求的重置互相覆盖）.
        await _databaseContext.AiModelQuota
            .Where(x => x.LimitId == limit.Id && x.PeriodEnd <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.UsedTokens, 0L)
                .SetProperty(x => x.TotalLimit, limit.LimitValue)
                .SetProperty(x => x.PeriodStart, now)
                .SetProperty(x => x.PeriodEnd, QuotaPeriodHelper.ComputePeriodEnd(now, limit.PeriodValue, limit.PeriodUnit))
                .SetProperty(x => x.LastResetTime, DateTime.Now),
                cancellationToken);

        var remaining = await _databaseContext.AiModelQuota
            .Where(x => x.LimitId == limit.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.UsedTokens, x => x.UsedTokens + totalTokens)
                .SetProperty(x => x.UpdateTime, now),
                cancellationToken);

        if (remaining == 0)
        {
            // 余额行缺失，补建后累加.
            await EnsureQuotaAsync(limit, cancellationToken);
            await _databaseContext.AiModelQuota
                .Where(x => x.LimitId == limit.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedTokens, x => x.UsedTokens + totalTokens), cancellationToken);
        }
    }

    private static string TruncateChannel(string providerKey)
    {
        if (string.IsNullOrEmpty(providerKey))
        {
            return "-";
        }

        return providerKey.Length <= 30 ? providerKey : providerKey[..30];
    }
}
