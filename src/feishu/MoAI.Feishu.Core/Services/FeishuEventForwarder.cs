using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeishuWss.Events;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Feishu.Models;
using MoAI.Feishu.Services;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书事件转发器：接收长连接事件后按 event_id 去重，解析事件归属的飞书应用绑定渠道，
/// 投递给该渠道类型注册的全部 <see cref="IFeishuEventHandler"/>（应用、知识库等业务模块实现）.
/// </summary>
[InjectOnSingleton]
public sealed partial class FeishuEventForwarder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeishuEventForwarder> _logger;
    private readonly FeishuEventDeduplicator _deduplicator = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuEventForwarder"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="logger">日志.</param>
    public FeishuEventForwarder(IServiceScopeFactory scopeFactory, ILogger<FeishuEventForwarder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 转发事件，在长连接收帧线程上调用：仅做 envelope 解析与去重，随后转线程池异步处理，不阻塞收帧.
    /// </summary>
    /// <param name="feishuAppId">事件归属的飞书应用记录 id.</param>
    /// <param name="context">长连接事件上下文.</param>
    public void Forward(Guid feishuAppId, EventContext context)
    {
        var envelope = context.TryParseEnvelope();
        if (envelope == null)
        {
            _logger.LogWarning("飞书事件 envelope 解析失败，丢弃，feishuAppId={FeishuAppId}.", feishuAppId);
            return;
        }

        var eventId = envelope.Header.EventId;
        if (string.IsNullOrEmpty(eventId) || !_deduplicator.TryBegin(eventId))
        {
            _logger.LogDebug("飞书事件重复投递，忽略，feishuAppId={FeishuAppId} eventId={EventId}.", feishuAppId, eventId);
            return;
        }

        // event 节点原文，交给处理器自行反序列化
        var eventPayload = envelope.Event.ValueKind == System.Text.Json.JsonValueKind.Undefined
            ? string.Empty
            : envelope.Event.GetRawText();

        _ = ProcessAsync(
            feishuAppId,
            envelope.Header.AppId,
            envelope.Header.TenantKey,
            eventId,
            envelope.Header.EventType,
            envelope.Header.CreateTime,
            eventPayload,
            context.CancellationToken);
    }

    private async Task ProcessAsync(
        Guid feishuAppId,
        string appId,
        string tenantKey,
        string eventId,
        string eventType,
        string createTime,
        string eventPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            // 事件到达时应用可能已被禁用或删除
            var appExists = await databaseContext.FeishuApps
                .AnyAsync(x => x.Id == feishuAppId && !x.IsDisable, cancellationToken);

            if (!appExists)
            {
                return;
            }

            // 同一飞书应用同时只绑定一个渠道，事件只投递给该渠道
            var binding = await databaseContext.FeishuAppBindings
                .Where(x => x.FeishuAppId == feishuAppId)
                .Select(x => new { x.ChannelType, x.ChannelId })
                .FirstOrDefaultAsync(cancellationToken);

            if (binding == null)
            {
                _logger.LogDebug("飞书应用未绑定渠道，事件忽略，feishuAppId={FeishuAppId} eventType={EventType}.", feishuAppId, eventType);
                return;
            }

            var channelType = (FeishuChannelType)binding.ChannelType;
            var handlers = scope.ServiceProvider.GetServices<IFeishuEventHandler>()
                .Where(x => x.ChannelType == channelType)
                .ToList();

            if (handlers.Count == 0)
            {
                _logger.LogWarning(
                    "渠道 {ChannelType} 没有注册飞书事件处理器，事件忽略，feishuAppId={FeishuAppId} eventType={EventType}.",
                    channelType,
                    feishuAppId,
                    eventType);
                return;
            }

            var message = new FeishuEventMessage
            {
                FeishuAppId = feishuAppId,
                AppId = appId,
                TenantKey = tenantKey,
                EventId = eventId,
                EventType = eventType,
                CreateTime = createTime,
                Payload = eventPayload,
                ChannelType = channelType,
                ChannelId = binding.ChannelId,
            };

            foreach (var handler in handlers)
            {
                try
                {
                    await handler.HandleAsync(message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "飞书事件处理器执行失败，handler={Handler} eventType={EventType} eventId={EventId} channelId={ChannelId}.",
                        handler.GetType().Name,
                        eventType,
                        eventId,
                        binding.ChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "飞书事件转发失败，feishuAppId={FeishuAppId} eventType={EventType} eventId={EventId}.",
                feishuAppId,
                eventType,
                eventId);
        }
    }
}
