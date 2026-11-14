using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.LoginSafety.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// 登录风控领域服务实现：规则判定 + 登录日志/风险事件落库 + IP 封禁 + Hangfire 异步通知（站内信/通知渠道）.
/// 所有持久化与通知均在评估内完成，评估异常不影响登录主流程.
/// </summary>
public class LoginRiskService : ILoginRiskService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILoginSafetySettingsService _settingsService;
    private readonly IIpLocationService _ipLocationService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<LoginRiskService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginRiskService"/> class.
    /// </summary>
    public LoginRiskService(DatabaseContext databaseContext, IRedisDatabase redisDatabase, ILoginSafetySettingsService settingsService, IIpLocationService ipLocationService, IBackgroundJobClient backgroundJobClient, ILogger<LoginRiskService> logger)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _settingsService = settingsService;
        _ipLocationService = ipLocationService;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CheckIpBanAsync(string ip, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return;
        }

        var cacheKey = $"ls:ipban:{ip}";
        // 写入方使用 StringSetAsync(key, "1") 原始值，读取必须用原始 StringGetAsync；
        // GetAsync<string> 走 JSON 反序列化，遇到原始 `1`（Number token）会抛 JsonException 导致登录 500
        var cached = await _redisDatabase.Database.StringGetAsync(cacheKey);
        if (!cached.IsNull && cached.HasValue)
        {
            throw new BusinessException("当前 IP 登录失败次数过多已被临时封禁，请稍后再试.") { StatusCode = 403 };
        }

        // Redis 未命中兜底查 DB（多实例场景 Redis 键过期与 DB 到期时间一致，此处仅补偿缓存丢失）
        var now = DateTimeOffset.Now;
        var banned = await _databaseContext.LoginIpBans.AsNoTracking()
            .AnyAsync(x => x.Ip == ip && !x.IsLifted && x.ExpireTime > now, cancellationToken);
        if (banned)
        {
            await _redisDatabase.Database.StringSetAsync(cacheKey, "1", TimeSpan.FromMinutes(5));
            throw new BusinessException("当前 IP 登录失败次数过多已被临时封禁，请稍后再试.") { StatusCode = 403 };
        }
    }

    /// <inheritdoc/>
    public async Task<RiskEvaluationResult> EvaluateAsync(LoginEventContext context, CancellationToken cancellationToken)
    {
        var none = new RiskEvaluationResult { RiskLevel = LoginRiskLevel.None };
        try
        {
            var settings = await _settingsService.GetAsync(cancellationToken);
            var geo = _ipLocationService.Resolve(context.Ip);
            var now = DateTimeOffset.Now;

            if (!context.IsSuccess)
            {
                return await EvaluateFailAsync(settings, context, geo, now, cancellationToken);
            }

            return await EvaluateSuccessAsync(settings, context, geo, now, cancellationToken);
        }
        catch (Exception ex)
        {
            // 风控属于旁路能力，任何内部异常都不能阻断登录
            _logger.LogError(ex, "登录风控评估异常（不影响登录主流程）：user={UserName}, ip={Ip}.", context.UserName, context.Ip);
            return none;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetIpFailCountAsync(string ip, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return 0;
        }

        // 写入方为 StringIncrementAsync 原始整数，读取必须用原始 StringGetAsync（同 CheckIpBanAsync 的 JSON 反序列化规避）
        var cached = await _redisDatabase.Database.StringGetAsync($"ls:ipfail:{ip}");
        return cached.HasValue && int.TryParse(cached.ToString(), out var count) ? count : 0;
    }

    /// <summary>
    /// 失败登录评估：R1（IP 爆破）/ R2（撞库喷洒）计数与自动封禁.
    /// </summary>
    private async Task<RiskEvaluationResult> EvaluateFailAsync(LoginSafetySettings settings, LoginEventContext context, IpLocationInfo geo, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await PersistLoginLogAsync(context, geo, LoginRiskLevel.None, Array.Empty<string>(), cancellationToken);

        if (!settings.Enabled || string.IsNullOrWhiteSpace(context.Ip))
        {
            return new RiskEvaluationResult { RiskLevel = LoginRiskLevel.None };
        }

        var (totalFail, distinctUsers) = await IncrementIpFailAsync(context.Ip, context.UserName, settings.IpFailWindowMinutes);

        string? ruleCode = null;
        if (totalFail >= settings.IpFailThreshold)
        {
            ruleCode = LoginRiskRule.IpBruteForce;
        }
        else if (distinctUsers >= settings.DistinctUserThreshold)
        {
            ruleCode = LoginRiskRule.IpSpray;
        }

        if (ruleCode == null)
        {
            return new RiskEvaluationResult { RiskLevel = LoginRiskLevel.None };
        }

        var expireTime = now.AddMinutes(settings.IpBanMinutes);
        await BanIpAsync(context.Ip, expireTime, $"auto:{ruleCode}", ruleCode == LoginRiskRule.IpBruteForce
            ? $"滑动窗口内登录失败 {totalFail} 次"
            : $"滑动窗口内对 {distinctUsers} 个不同用户名登录失败", cancellationToken);

        // 封禁属于高风险事件，记录风险事件并推送已配置的通知渠道
        var reason = LoginRiskRule.GetTitle(ruleCode);
        var eventId = await PersistRiskEventAsync(context, geo, ruleCode, LoginRiskLevel.High, reason, now, cancellationToken);
        if (settings.NotifyChannelIds.Count > 0)
        {
            EnqueueNotify(eventId, LoginRiskNotifyJob.Targets.Channels);
        }

        return new RiskEvaluationResult { RiskLevel = LoginRiskLevel.High, Reasons = new[] { reason }, IpBanned = true };
    }

    /// <summary>
    /// 成功登录评估：R3 新 IP / R4 异地 / R5 疾速旅行 / R6 管理员新环境.
    /// </summary>
    private async Task<RiskEvaluationResult> EvaluateSuccessAsync(LoginSafetySettings settings, LoginEventContext context, IpLocationInfo geo, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var rules = new List<string>();
        var level = LoginRiskLevel.None;

        // 历史判定必须先于当前日志落库，否则本次记录会污染「上次登录」口径
        var lastSuccess = await _databaseContext.LoginLogs.AsNoTracking()
            .Where(x => x.UserId == context.UserId && x.IsSuccess)
            .OrderByDescending(x => x.CreateTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings.Enabled && context.UserId > 0)
        {
            // R3 新 IP：回看期内该用户无此 IP 成功登录记录
            if (settings.NewIpDays > 0 && !string.IsNullOrWhiteSpace(context.Ip))
            {
                var since = now.AddDays(-settings.NewIpDays);
                var seenIp = await _databaseContext.LoginLogs.AsNoTracking()
                    .AnyAsync(x => x.UserId == context.UserId && x.Ip == context.Ip && x.IsSuccess && x.CreateTime >= since, cancellationToken);
                if (!seenIp)
                {
                    reasons.Add($"新 IP 登录：{context.Ip}");
                    rules.Add(LoginRiskRule.NewIp);
                    level = LoginRiskLevel.Medium;
                }
            }

            // R4 异地：与近 30 天最近一次成功登录省份不同（地理库可用时）
            if (_ipLocationService.IsAvailable && geo.Province.Length > 0)
            {
                var since = now.AddDays(-30);
                var usual = await _databaseContext.LoginLogs.AsNoTracking()
                    .Where(x => x.UserId == context.UserId && x.IsSuccess && x.Province != string.Empty && x.CreateTime >= since)
                    .OrderByDescending(x => x.CreateTime)
                    .Select(x => x.Province)
                    .FirstOrDefaultAsync(cancellationToken);

                if (usual != null && usual != geo.Province)
                {
                    reasons.Add($"异地登录：本次 {geo.Province}，常用 {usual}");
                    rules.Add(LoginRiskRule.NewLocation);
                    if (level < LoginRiskLevel.Medium)
                    {
                        level = LoginRiskLevel.Medium;
                    }
                }

                // R5 疾速旅行：与上次成功登录省份不同且间隔过短
                if (lastSuccess != null && lastSuccess.Province.Length > 0
                    && lastSuccess.Province != geo.Province
                    && (now - lastSuccess.CreateTime).TotalMinutes < settings.TravelWindowMinutes)
                {
                    reasons.Add($"疾速旅行：{lastSuccess.CreateTime:HH:mm} 在 {lastSuccess.Province}，{now:HH:mm} 出现在 {geo.Province}");
                    rules.Add(LoginRiskRule.ImpossibleTravel);
                    level = LoginRiskLevel.High;
                }
            }

            // R6 管理员账号新环境登录：中风险升级为高风险
            if (context.IsAdmin && level >= LoginRiskLevel.Medium)
            {
                reasons.Add("管理员账号新环境登录");
                rules.Add(LoginRiskRule.AdminNewEnvironment);
                level = LoginRiskLevel.High;
            }
        }

        await PersistLoginLogAsync(context, geo, level, reasons, cancellationToken);

        // 成功登录说明该 IP 是合法来源，清除其失败计数（窗口计数仅用于失败累积判定）
        if (!string.IsNullOrWhiteSpace(context.Ip))
        {
            await _redisDatabase.Database.KeyDeleteAsync($"ls:ipfail:{context.Ip}");
            await _redisDatabase.Database.KeyDeleteAsync($"ls:ipusers:{context.Ip}");
        }

        if (level < LoginRiskLevel.Medium)
        {
            return new RiskEvaluationResult { RiskLevel = level, Reasons = reasons };
        }

        // 主规则取最高严重级（R5 > R4 > R3），管理员升级单独作为事件规则
        var primaryRule = rules.Contains(LoginRiskRule.ImpossibleTravel)
            ? LoginRiskRule.ImpossibleTravel
            : rules.Contains(LoginRiskRule.NewLocation)
                ? LoginRiskRule.NewLocation
                : rules.Contains(LoginRiskRule.AdminNewEnvironment) ? LoginRiskRule.AdminNewEnvironment : LoginRiskRule.NewIp;

        var eventId = await PersistRiskEventAsync(context, geo, primaryRule, level, string.Join("；", reasons), now, cancellationToken, lastProvince: lastSuccess?.Province ?? string.Empty);

        // 中/高风险登录成功：站内信提醒受影响用户（固定逻辑，无开关）
        EnqueueNotify(eventId, LoginRiskNotifyJob.Targets.User);

        if (level == LoginRiskLevel.High && settings.NotifyChannelIds.Count > 0)
        {
            EnqueueNotify(eventId, LoginRiskNotifyJob.Targets.Channels);
        }

        return new RiskEvaluationResult { RiskLevel = level, Reasons = reasons };
    }

    /// <summary>
    /// 累加 IP 失败计数（总失败数 + 不同用户名集合），滑动窗口.
    /// </summary>
    private async Task<(int TotalFail, int DistinctUsers)> IncrementIpFailAsync(string ip, string userName, int windowMinutes)
    {
        var failKey = $"ls:ipfail:{ip}";
        var usersKey = $"ls:ipusers:{ip}";

        var total = await _redisDatabase.Database.StringIncrementAsync(failKey);
        await _redisDatabase.Database.KeyExpireAsync(failKey, TimeSpan.FromMinutes(windowMinutes));

        var window = TimeSpan.FromMinutes(windowMinutes);
        if (!string.IsNullOrWhiteSpace(userName))
        {
            await _redisDatabase.Database.SetAddAsync(usersKey, userName);
            await _redisDatabase.Database.KeyExpireAsync(usersKey, window);
        }

        var distinct = await _redisDatabase.Database.SetLengthAsync(usersKey);
        return ((int)total, (int)distinct);
    }

    /// <summary>
    /// 封禁 IP：写 DB 封禁记录 + Redis 判定缓存.
    /// </summary>
    private async Task BanIpAsync(string ip, DateTimeOffset expireTime, string sourceRule, string reason, CancellationToken cancellationToken)
    {
        // 已有生效封禁时不重复写入
        var exists = await _databaseContext.LoginIpBans
            .AnyAsync(x => x.Ip == ip && !x.IsLifted && x.ExpireTime > DateTimeOffset.Now, cancellationToken);
        if (exists)
        {
            return;
        }

        _databaseContext.LoginIpBans.Add(new LoginIpBanEntity
        {
            Id = Guid.CreateVersion7(),
            Ip = ip,
            Reason = reason,
            SourceRule = sourceRule,
            ExpireTime = expireTime,
            IsLifted = false,
            LiftUserId = 0,
            CreateTime = DateTimeOffset.Now
        });
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var ttl = expireTime - DateTimeOffset.Now;
        if (ttl > TimeSpan.Zero)
        {
            await _redisDatabase.Database.StringSetAsync($"ls:ipban:{ip}", "1", ttl);
        }

        _logger.LogWarning("登录风控已封禁 IP：{Ip}，原因：{Reason}，到期：{ExpireTime}.", ip, reason, expireTime);
    }

    /// <summary>
    /// 登录日志落库（每次登录尝试都记录，风控开关关闭时也记录）.
    /// </summary>
    private async Task PersistLoginLogAsync(LoginEventContext context, IpLocationInfo geo, LoginRiskLevel riskLevel, IReadOnlyList<string> reasons, CancellationToken cancellationToken)
    {
        _databaseContext.LoginLogs.Add(new LoginLogEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = context.UserId,
            UserName = Truncate(context.UserName, 200),
            LoginType = (int)context.LoginType,
            IsSuccess = context.IsSuccess,
            FailReason = context.FailReason,
            Ip = Truncate(context.Ip, 64),
            Country = geo.Country,
            Province = geo.Province,
            City = geo.City,
            Isp = geo.Isp,
            UserAgent = Truncate(context.UserAgent, 500),
            RiskLevel = (int)riskLevel,
            RiskReasons = reasons.Count > 0 ? JsonSerializer.Serialize(reasons) : string.Empty,
            CreateTime = DateTimeOffset.Now
        });
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 风险事件落库，返回事件 id.
    /// </summary>
    private async Task<Guid> PersistRiskEventAsync(LoginEventContext context, IpLocationInfo geo, string ruleCode, LoginRiskLevel level, string summary, DateTimeOffset now, CancellationToken cancellationToken, string lastProvince = "")
    {
        var detail = new Dictionary<string, string>
        {
            ["email"] = context.Email,
            ["userAgent"] = context.UserAgent,
            ["loginType"] = context.LoginType.ToString(),
            ["isp"] = geo.Isp,
            ["lastProvince"] = lastProvince,
            ["time"] = now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        var entity = new LoginRiskEventEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = context.UserId,
            UserName = Truncate(context.UserName, 200),
            RuleCode = ruleCode,
            RiskLevel = (int)level,
            Summary = Truncate(summary, 1000),
            Detail = JsonSerializer.Serialize(detail),
            IsRead = false,
            Ip = Truncate(context.Ip, 64),
            Province = geo.Province,
            City = geo.City,
            CreateTime = now
        };

        _databaseContext.LoginRiskEvents.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <summary>
    /// 投递通知任务（Hangfire 异步，失败自动重试）.
    /// </summary>
    private void EnqueueNotify(Guid riskEventId, string target)
    {
        _backgroundJobClient.Enqueue<LoginRiskNotifyJob>(job => job.SendAsync(riskEventId, target, CancellationToken.None));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
