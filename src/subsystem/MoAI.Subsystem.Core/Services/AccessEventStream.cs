using System.Globalization;
using System.Text.Json;
using Maomi;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Events;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 接入事件短期补发窗口：Redis Stream 保存最近事件，供断线重连与 REST 补发读取.
/// </summary>
[InjectOnScoped]
public class AccessEventStream
{
    private const string StreamKey = "subsys:events";
    private const int MaxLength = 10000;
    private const int MaxScan = 2000;

    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<AccessEventStream> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessEventStream"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="logger">日志.</param>
    public AccessEventStream(IRedisDatabase redisDatabase, ILogger<AccessEventStream> logger)
    {
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <summary>
    /// 追加事件到 Stream，返回 Stream ID；Redis 不可用时返回空串（由调用方回落 Guid）.
    /// </summary>
    /// <param name="event">事件.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>事件 id.</returns>
    public async Task<string> AppendAsync(AccessEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = new NameValueEntry[]
            {
                new("event", @event.Event),
                new("audience", ((int)@event.Audience).ToString(CultureInfo.InvariantCulture)),
                new("target", @event.TargetAppId?.ToString("N") ?? string.Empty),
                new("source", @event.Source ?? string.Empty),
                new("suppress", @event.SuppressEcho ? "1" : "0"),
                new("time", @event.Time.ToString("O", CultureInfo.InvariantCulture)),
                new("data", JsonSerializer.Serialize(@event.Data)),
            };

            var id = await _redisDatabase.StreamAddAsync(StreamKey, entries, null, MaxLength, true);
            return id.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接入事件写入 Redis Stream 失败，实时补发能力降级. Event={Event}", @event.Event);
            return string.Empty;
        }
    }

    /// <summary>
    /// 读取某接入在指定游标之后的事件.
    /// </summary>
    /// <param name="appId">接入 id.</param>
    /// <param name="afterId">游标（空=需要首次全量对账）.</param>
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

        if (string.IsNullOrWhiteSpace(afterId))
        {
            // 首次连接/首次补发：没有游标，接收方必须先做全量对账
            return (Array.Empty<AccessEventItem>(), false, true, await GetOldestIdAsync(cancellationToken));
        }

        try
        {
            var oldest = await _redisDatabase.StreamRangeAsync(StreamKey, "-", "+", 1, Order.Ascending);
            var oldestId = oldest.Length > 0 ? oldest[0].Id.ToString() : null;

            if (oldestId != null && CompareStreamIds(oldestId, afterId) > 0)
            {
                // 游标早于窗口起点：中间事件已被裁掉
                return (Array.Empty<AccessEventItem>(), false, true, oldestId);
            }

            var raw = await _redisDatabase.StreamRangeAsync(StreamKey, "(" + afterId, "+", Math.Min(MaxScan, limit + 1), Order.Ascending);
            var items = new List<AccessEventItem>(raw.Length);
            foreach (var entry in raw)
            {
                var item = ToItem(entry, appId);
                if (item != null)
                {
                    items.Add(item);
                    if (items.Count > limit)
                    {
                        break;
                    }
                }
            }

            var hasMore = items.Count > limit;
            if (hasMore)
            {
                items.RemoveAt(items.Count - 1);
            }

            return (items, hasMore, false, oldestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取接入事件补发窗口失败，要求全量对账. AppId={AppId}", appId);
            return (Array.Empty<AccessEventItem>(), false, true, null);
        }
    }

    private async Task<string?> GetOldestIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            var oldest = await _redisDatabase.StreamRangeAsync(StreamKey, "-", "+", 1, Order.Ascending);
            return oldest.Length > 0 ? oldest[0].Id.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static AccessEventItem? ToItem(StreamEntry entry, Guid appId)
    {
        var fields = entry.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString(), StringComparer.Ordinal);
        if (!fields.TryGetValue("event", out var eventName))
        {
            return null;
        }

        var audience = fields.GetValueOrDefault("audience") == "0"
            ? AccessEventAudience.AllSystemApps
            : AccessEventAudience.TargetApp;

        if (audience == AccessEventAudience.TargetApp
            && (!Guid.TryParse(fields.GetValueOrDefault("target"), out var target) || target != appId))
        {
            return null;
        }

        var source = fields.GetValueOrDefault("source");
        if (fields.GetValueOrDefault("suppress") == "1"
            && string.Equals(source, appId.ToString("N"), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        DateTimeOffset time = default;
        if (fields.TryGetValue("time", out var timeText))
        {
            DateTimeOffset.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time);
        }

        IReadOnlyDictionary<string, object?>? data = null;
        if (fields.TryGetValue("data", out var dataText) && !string.IsNullOrWhiteSpace(dataText))
        {
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, object?>>(dataText);
            }
            catch (JsonException)
            {
                data = null;
            }
        }

        return new AccessEventItem
        {
            Id = entry.Id.ToString(),
            Event = eventName,
            Time = time,
            Source = string.IsNullOrEmpty(source) ? null : source,
            Data = data,
        };
    }

    private static int CompareStreamIds(string left, string right)
    {
        var leftParts = left.Split('-');
        var rightParts = right.Split('-');
        if (leftParts.Length != 2 || rightParts.Length != 2)
        {
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        if (!long.TryParse(leftParts[0], out var leftMs) || !long.TryParse(rightParts[0], out var rightMs))
        {
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        if (leftMs != rightMs)
        {
            return leftMs.CompareTo(rightMs);
        }

        _ = long.TryParse(leftParts[1], out var leftSeq);
        _ = long.TryParse(rightParts[1], out var rightSeq);
        return leftSeq.CompareTo(rightSeq);
    }
}
