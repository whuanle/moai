using System.Text.Json;
using Maomi.MQ;
using Maomi.MQ.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Events;
using MoAI.Subsystem.Events;
using MoAI.Subsystem.Hubs;

namespace MoAI.Subsystem.Consumers;

/// <summary>
/// 接入事件消费者：解析受众 → 查启用中的系统级接入 → 按 appId Group 推送.
/// </summary>
[Consumer(AccessEventPushMessage.QueueName, Qos = 1)]
public class AccessEventPushConsumer : IConsumer<AccessEventPushMessage>
{
    private const string OnEvent = "onEvent";

    private readonly IHubContext<SubsysEventHub> _hubContext;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<AccessEventPushConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessEventPushConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">Hub 上下文.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="logger">日志.</param>
    public AccessEventPushConsumer(
        IHubContext<SubsysEventHub> hubContext,
        DatabaseContext databaseContext,
        ILogger<AccessEventPushConsumer> logger)
    {
        _hubContext = hubContext;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, AccessEventPushMessage message)
    {
        var item = new AccessEventItem
        {
            Id = message.Id,
            Event = message.Event,
            Time = message.Time == default ? DateTimeOffset.UtcNow : message.Time,
            Source = message.Source,
            Data = message.Data,
        };

        if (message.Audience == AccessEventAudience.TargetApp)
        {
            if (message.TargetAppId is not Guid target)
            {
                return;
            }

            if (!IsFinalDelivery(message) && !(await GetEnabledAppIdsAsync()).Contains(target))
            {
                return;
            }

            await _hubContext.Clients.Group(AccessEventHubGroups.ForApp(target)).SendAsync(OnEvent, item);
            return;
        }

        var targets = new HashSet<Guid>(await GetEnabledAppIdsAsync());
        if (message.SuppressEcho && Guid.TryParse(message.Source, out var source))
        {
            targets.Remove(source);
        }

        foreach (var target in targets)
        {
            await _hubContext.Clients.Group(AccessEventHubGroups.ForApp(target)).SendAsync(OnEvent, item);
        }
    }

    /// <inheritdoc/>
    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, AccessEventPushMessage message)
    {
        _logger.LogError(ex, "接入事件推送失败. Event={Event}, RetryCount={RetryCount}", message.Event, retryCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, AccessEventPushMessage? message, Exception? ex)
    {
        _logger.LogError(ex, "接入事件推送重试耗尽，放弃实时推送（由对账兜底）. Event={Event}", message?.Event);
        await Task.CompletedTask;
        return ConsumerState.Ack;
    }

    private async Task<HashSet<Guid>> GetEnabledAppIdsAsync()
    {
        // 不做进程内缓存：新建/启用接入必须立即收到后续事件，接入数量很小，单次索引查询成本可接受
        var ids = await _databaseContext.AccessApps
            .AsNoTracking()
            .Where(x => x.Level == AccessAppLevel.System && x.Status == AccessAppStatus.Enabled)
            .Select(x => x.Id)
            .ToListAsync();

        return ids.ToHashSet();
    }

    private static bool IsFinalDelivery(AccessEventPushMessage message)
    {
        if (message.Event == AccessEventNames.AccessAppDeleted)
        {
            return true;
        }

        if (message.Event != AccessEventNames.AccessAppStatusChanged || message.Data == null)
        {
            return false;
        }

        return TryReadStatus(message.Data) == "disabled";
    }

    private static string? TryReadStatus(IReadOnlyDictionary<string, object?> data)
    {
        if (!data.TryGetValue("status", out var value))
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => value?.ToString(),
        };
    }
}
