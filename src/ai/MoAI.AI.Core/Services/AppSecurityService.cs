using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Aggregates;

namespace MoAI.AI.Services;

/// <summary>
/// 应用内容脱敏策略读取服务：按应用加载 <see cref="AppSecurityPolicy"/>（app_security_config），
/// 同一作用域内记忆化（一次对话装配 / 一轮流程执行只查一次库）.
/// </summary>
[InjectOnScoped]
public sealed class AppSecurityService
{
    private readonly DatabaseContext _databaseContext;
    private readonly Dictionary<Guid, AppSecurityPolicy> _cache = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSecurityService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public AppSecurityService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 加载应用脱敏策略；未配置或未启用返回 <see cref="AppSecurityPolicy.Disabled"/>（恒等脱敏）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>脱敏策略.</returns>
    public async Task<AppSecurityPolicy> GetPolicyAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(appId, out var cached))
        {
            return cached;
        }

        var entity = await _databaseContext.AppSecurityConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken).ConfigureAwait(false);
        var policy = AppSecurityPolicy.Parse(entity);
        _cache[appId] = policy;
        return policy;
    }
}
