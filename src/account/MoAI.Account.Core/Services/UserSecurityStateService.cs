using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Services;
using MoAI.Database;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Account.Services;

/// <summary>
/// <inheritdoc cref="IUserSecurityStateService"/>
/// </summary>
public class UserSecurityStateService : IUserSecurityStateService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private static readonly RedisValue[] Fields = ["disabled", "deleted", "version"];

    /// <summary>
    /// 单调写：仅当新版本 >= 当前版本时覆盖，避免并发请求在 DB 提交前读到旧值后把缓存回写成旧版本.
    /// </summary>
    private const string UpsertScript = """
        local current = redis.call('HGET', KEYS[1], 'version')
        if (not current) or (tonumber(ARGV[1]) >= tonumber(current)) then
          redis.call('HSET', KEYS[1], 'disabled', ARGV[2], 'deleted', ARGV[3], 'version', ARGV[1])
          redis.call('EXPIRE', KEYS[1], ARGV[4])
          return 1
        end
        return 0
        """;

    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<UserSecurityStateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSecurityStateService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="logger">日志.</param>
    public UserSecurityStateService(DatabaseContext databaseContext, IRedisDatabase redisDatabase, ILogger<UserSecurityStateService> logger)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserSecurityState> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var key = BuildKey(userId);
        try
        {
            var values = await _redisDatabase.Database.HashGetAsync(key, Fields);
            if (values.Length == Fields.Length && values[2].HasValue)
            {
                return new UserSecurityState
                {
                    UserId = userId,
                    IsDisable = ParseBool(values[0]),
                    IsDeleted = ParseBool(values[1]),
                    AuthVersion = ParseLong(values[2]),
                };
            }
        }
        catch (Exception ex)
        {
            // Redis 异常不阻断鉴权：回库读取，恢复后由写穿透重新回填
            _logger.LogWarning(ex, "读取用户安全态缓存失败，回退数据库. UserId={UserId}", userId);
        }

        var row = await _databaseContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.IsDisable, x.IsDeleted, x.AuthVersion })
            .FirstOrDefaultAsync(cancellationToken);

        var state = row == null
            ? new UserSecurityState { UserId = userId, IsDisable = true, IsDeleted = true, AuthVersion = 0 }
            : new UserSecurityState
            {
                UserId = userId,
                IsDisable = row.IsDisable,
                IsDeleted = row.IsDeleted > 0,
                AuthVersion = row.AuthVersion,
            };

        await SetAsync(state.UserId, state.IsDisable, state.IsDeleted, state.AuthVersion, cancellationToken);
        return state;
    }

    /// <inheritdoc/>
    public async Task SetAsync(long userId, bool isDisable, bool isDeleted, long authVersion, CancellationToken cancellationToken)
    {
        try
        {
            await _redisDatabase.Database.ScriptEvaluateAsync(
                UpsertScript,
                [BuildKey(userId)],
                [
                    authVersion.ToString(CultureInfo.InvariantCulture),
                    isDisable ? "1" : "0",
                    isDeleted ? "1" : "0",
                    ((long)CacheTtl.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                ]);
        }
        catch (Exception ex)
        {
            // 缓存写失败只影响命中率：DB 仍是真源，下一次读取会回填
            _logger.LogWarning(ex, "写入用户安全态缓存失败. UserId={UserId}, Version={Version}", userId, authVersion);
        }
    }

    private static RedisKey BuildKey(long userId) => $"userstate:security:{userId}";

    private static bool ParseBool(RedisValue value)
        => value.HasValue && (value == "1" || string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase));

    private static long ParseLong(RedisValue value)
        => value.HasValue && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
