using System.Collections;
using System.Text;
using System.Text.Json;
using Maomi;
using Maomi.MQ;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Events;
using MoAI.Subsystem.Events;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 接入事件发布器：写 Redis Stream（补发窗口）→ 发 RabbitMQ（实时推送）；
/// 发布失败只记日志，不反向影响业务请求.
/// </summary>
[InjectOnScoped]
public class AccessEventPublisher : IAccessEventPublisher
{
    private const int SmallPayloadBytes = 2 * 1024;
    private const int HardPayloadBytes = 8 * 1024;
    private const int MaxArrayItems = 50;
    private const string EchoHeader = "X-MoAI-Event-Echo";

    private readonly AccessEventStream _eventStream;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AccessEventPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessEventPublisher"/> class.
    /// </summary>
    /// <param name="eventStream">事件补发窗口.</param>
    /// <param name="messagePublisher">MQ 发布器.</param>
    /// <param name="httpContextAccessor">HTTP 上下文（识别来源接入/回声开关）.</param>
    /// <param name="logger">日志.</param>
    public AccessEventPublisher(
        AccessEventStream eventStream,
        IMessagePublisher messagePublisher,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AccessEventPublisher> logger)
    {
        _eventStream = eventStream;
        _messagePublisher = messagePublisher;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(
        string eventName,
        AccessEventAudience audience,
        Guid? targetAppId = null,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventName)
            || (audience == AccessEventAudience.TargetApp && targetAppId == null))
        {
            _logger.LogWarning("接入事件参数不完整，已跳过. Event={Event}, Audience={Audience}", eventName, audience);
            return;
        }

        try
        {
            var normalizedData = NormalizeData(data);
            var dataJson = JsonSerializer.Serialize(normalizedData);
            var payloadBytes = Encoding.UTF8.GetByteCount(dataJson);
            if (payloadBytes > HardPayloadBytes)
            {
                _logger.LogError("接入事件载荷超过硬上限，已拒绝发送. Event={Event}, Bytes={Bytes}", eventName, payloadBytes);
                return;
            }

            if (payloadBytes > SmallPayloadBytes)
            {
                _logger.LogWarning("接入事件载荷偏大. Event={Event}, Bytes={Bytes}", eventName, payloadBytes);
            }

            var httpContext = _httpContextAccessor.HttpContext;
            var source = ReadSource(httpContext);
            var suppressEcho = string.Equals(
                httpContext?.Request.Headers[EchoHeader].ToString(),
                "false",
                StringComparison.OrdinalIgnoreCase);

            var @event = new AccessEvent(
                Id: string.Empty,
                Event: eventName,
                Audience: audience,
                TargetAppId: targetAppId,
                Source: source,
                SuppressEcho: suppressEcho,
                Time: DateTimeOffset.UtcNow,
                Data: normalizedData);

            var id = await _eventStream.AppendAsync(@event, cancellationToken);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.CreateVersion7().ToString("N");
            }

            await _messagePublisher.AutoPublishAsync(new AccessEventPushMessage
            {
                Id = id,
                Event = @event.Event,
                Audience = @event.Audience,
                TargetAppId = @event.TargetAppId,
                Source = @event.Source,
                SuppressEcho = @event.SuppressEcho,
                Time = @event.Time,
                Data = @event.Data,
            });
        }
        catch (Exception ex)
        {
            // 业务已提交，推送是尽力而为；失败由接收方全量对账兜底
            _logger.LogError(ex, "接入事件发布失败. Event={Event}, Audience={Audience}, Target={Target}", eventName, audience, targetAppId);
        }
    }

    private static string? ReadSource(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue(SubsystemAuthDefaults.CallerContextItemKey, out var value) == true
            && value is SubsysCallerContext caller)
        {
            return caller.Id.ToString("N");
        }

        return null;
    }

    private static IReadOnlyDictionary<string, object?>? NormalizeData(IReadOnlyDictionary<string, object?>? data)
    {
        if (data == null || data.Count == 0)
        {
            return data;
        }

        var result = new Dictionary<string, object?>(data.Count + 1, StringComparer.Ordinal);
        var truncated = false;

        foreach (var (key, value) in data)
        {
            if (value is string || value is not IEnumerable enumerable)
            {
                result[key] = value;
                continue;
            }

            var items = enumerable.Cast<object?>().ToList();
            if (items.Count <= MaxArrayItems)
            {
                result[key] = items.ToArray();
                continue;
            }

            var countKey = key.EndsWith("Ids", StringComparison.Ordinal)
                ? key[..^3] + "Count"
                : key + "Count";
            result[countKey] = items.Count;
            truncated = true;
        }

        if (truncated)
        {
            result["truncated"] = true;
        }

        return result;
    }
}
