using System.Globalization;
using System.Text.Json;
using Maomi;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Events;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 接入事件短期补发索引：按 UTC 天分代，每代用 Redis Hash（实体最新事件）+ Sorted Set（序号索引）保存。
/// 同一事件名 + 同一主体只保留最新一条（禁用→启用→禁用 只留最后一条）；跨天换键，旧代 2 天后自动过期。
/// 多 MoAI 实例共享同一 Redis，序号用 INCR 分配，写入用 Lua 原子完成。
/// </summary>
[InjectOnScoped]
public class AccessEventStore
{
    private const string KeyPrefix = "moai:subsys:evt";
    private static readonly TimeSpan Retention = TimeSpan.FromDays(2);
    private const long MaxCursorScan = 2000;

    /// <summary>
    /// 发布并写入当前代索引：INCR 取序号 → 覆盖实体最新事件 → ZADD 索引 → 续期.
    /// </summary>
    private const string AppendScript = """
        local seq = redis.call('INCR', KEYS[3])
        local id = ARGV[4] .. ':' .. seq
        local json = string.gsub(ARGV[1], '__ID__', id)
        redis.call('HSET', KEYS[1], ARGV[2], json)
        redis.call('ZADD', KEYS[2], seq, ARGV[2])
        redis.call('PEXPIRE', KEYS[1], ARGV[3])
        redis.call('PEXPIRE', KEYS[2], ARGV[3])
        redis.call('PEXPIRE', KEYS[3], ARGV[3])
        return seq
        """;

    private readonly IDatabase _database;
    private readonly ILogger<AccessEventStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessEventStore"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库（取底层 IDatabase 使用 Hash/ZSet/Lua）.</param>
    /// <param name="logger">日志.</param>
    public AccessEventStore(IRedisDatabase redisDatabase, ILogger<AccessEventStore> logger)
    {
        _database = redisDatabase.Database;
        _logger = logger;
    }

    /// <summary>
    /// 写入事件并返回事件 id（形如 <c>yyyyMMdd:seq</c>）；Redis 不可用时返回空串.
    /// </summary>
    /// <param name="event">事件（Id 占位）.</param>
    /// <param name="entityKey">实体键：事件名 + 主体 id，用于同实体最新事件覆盖.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件 id.</returns>
    public async Task<string> AppendAsync(AccessEvent @event, string entityKey, CancellationToken cancellationToken = default)
    {
        var epoch = CurrentEpoch();
        var (latestKey, indexKey, seqKey) = Keys(epoch);
        var item = new AccessEventItem
        {
            Id = "__ID__",
            Event = @event.Event,
            Time = @event.Time,
            Source = @event.Source,
            Data = @event.Data,
        };
        var record = new StoredEvent
        {
            Item = item,
            Audience = (int)@event.Audience,
            Target = @event.TargetAppId?.ToString("N"),
            Suppress = @event.SuppressEcho,
        };
        var field = BuildField(@event.Audience, @event.TargetAppId, entityKey);
        var json = JsonSerializer.Serialize(record);

        try
        {
            var result = await _database.ScriptEvaluateAsync(
                AppendScript,
                [latestKey, indexKey, seqKey],
                [json, field, ((long)Retention.TotalMilliseconds).ToString(CultureInfo.InvariantCulture), epoch]);
            return $"{epoch}:{result}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接入事件写入 Redis 索引失败，实时补发能力降级. Event={Event}", @event.Event);
            return string.Empty;
        }
    }

    /// <summary>
    /// 读取某接入在游标之后的最新事件（同一实体只返回最新一条）.
    /// </summary>
    /// <param name="appId">接入 id.</param>
    /// <param name="afterId">游标（<c>yyyyMMdd:seq</c>；空/跨天/非法均要求全量对账）.</param>
    /// <param name="limit">返回上限.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>补发结果.</returns>
    public async Task<(IReadOnlyList<AccessEventItem> Items, bool HasMore, bool ResyncRequired, string? OldestId)> ReadAsync(
        Guid appId,
        string? afterId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var epoch = CurrentEpoch();
        var (latestKey, indexKey, seqKey) = Keys(epoch);

        if (string.IsNullOrWhiteSpace(afterId))
        {
            return (Array.Empty<AccessEventItem>(), false, true, await GetOldestIdAsync(epoch, indexKey));
        }

        if (!TryParseCursor(afterId, out var cursorEpoch, out var cursorSeq) || cursorEpoch != epoch)
        {
            // 跨天/旧格式游标：代已切换，直接全量对账
            return (Array.Empty<AccessEventItem>(), false, true, await GetOldestIdAsync(epoch, indexKey));
        }

        try
        {
            var currentSeq = await _database.StringGetAsync(seqKey);
            if (!currentSeq.HasValue || (long)currentSeq < cursorSeq)
            {
                // Redis 键被清理/重建，旧游标失效
                return (Array.Empty<AccessEventItem>(), false, true, await GetOldestIdAsync(epoch, indexKey));
            }

            var members = await _database.SortedSetRangeByScoreAsync(
                indexKey,
                cursorSeq + 1,
                double.PositiveInfinity,
                Exclude.None,
                Order.Ascending,
                0,
                Math.Min(MaxCursorScan, limit + 1));

            if (members.Length == 0)
            {
                return (Array.Empty<AccessEventItem>(), false, false, await GetOldestIdAsync(epoch, indexKey));
            }

            var hasMore = members.Length > limit;
            var selected = hasMore ? members[..(int)limit] : members;
            var values = await _database.HashGetAsync(latestKey, selected);
            var items = new List<AccessEventItem>(selected.Length);

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].IsNullOrEmpty)
                {
                    continue;
                }

                StoredEvent? record;
                try
                {
                    record = JsonSerializer.Deserialize<StoredEvent>(values[i].ToString());
                }
                catch (JsonException)
                {
                    continue;
                }

                if (record?.Item == null)
                {
                    continue;
                }

                if (record.Audience == (int)AccessEventAudience.TargetApp
                    && !string.Equals(record.Target, appId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (record.Suppress
                    && string.Equals(record.Item.Source, appId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                items.Add(record.Item);
            }

            return (items, hasMore, false, await GetOldestIdAsync(epoch, indexKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取接入事件补发索引失败，要求全量对账. AppId={AppId}", appId);
            return (Array.Empty<AccessEventItem>(), false, true, null);
        }
    }

    private async Task<string?> GetOldestIdAsync(string epoch, RedisKey indexKey)
    {
        try
        {
            var first = await _database.SortedSetRangeByRankWithScoresAsync(indexKey, 0, 0, Order.Ascending);
            return first.Length == 0 ? null : $"{epoch}:{(long)first[0].Score}";
        }
        catch
        {
            return null;
        }
    }

    private static (RedisKey Latest, RedisKey Index, RedisKey Seq) Keys(string epoch)
    {
        var prefix = $"{KeyPrefix}:{epoch}";
        return ($"{prefix}:latest", $"{prefix}:index", $"{prefix}:seq");
    }

    private static string CurrentEpoch() => DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string BuildField(AccessEventAudience audience, Guid? targetAppId, string entityKey)
    {
        var target = audience == AccessEventAudience.TargetApp && targetAppId.HasValue
            ? targetAppId.Value.ToString("N")
            : "-";
        return $"{(int)audience}|{target}|{entityKey}";
    }

    private static bool TryParseCursor(string cursor, out string epoch, out long seq)
    {
        epoch = string.Empty;
        seq = 0;
        var parts = cursor.Split(':');
        return parts.Length == 2
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out seq)
            && seq > 0
            && (epoch = parts[0]).Length == 8;
    }

    private sealed class StoredEvent
    {
        public AccessEventItem? Item { get; set; }

        public int Audience { get; set; }

        public string? Target { get; set; }

        public bool Suppress { get; set; }
    }
}
