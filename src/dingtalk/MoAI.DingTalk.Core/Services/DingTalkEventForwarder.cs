using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DingTalkStream.Core;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.DingTalk.Models;

namespace MoAI.DingTalk.Services;

/// <summary>
/// 钉钉事件转发器：接收 Stream 长连接推送后按 message_id 去重，解析推送归属的钉钉应用绑定渠道，
/// 投递给该渠道类型注册的全部 <see cref="IDingTalkEventHandler"/>（应用等业务模块实现）.
/// <para>收到推送即回复 ack（钉钉 5 秒内未收到回复会重发），随后转线程池异步处理，不阻塞收帧.</para>
/// </summary>
[InjectOnSingleton]
public sealed partial class DingTalkEventForwarder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DingTalkEventForwarder> _logger;
    private readonly DingTalkEventDeduplicator _deduplicator = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkEventForwarder"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="logger">日志.</param>
    public DingTalkEventForwarder(IServiceScopeFactory scopeFactory, ILogger<DingTalkEventForwarder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 转发推送，在长连接收帧线程上调用：先回复 ack，再解析去重，随后转线程池异步处理，不阻塞收帧.
    /// </summary>
    /// <param name="dingTalkAppId">推送归属的钉钉应用记录 id.</param>
    /// <param name="appKey">钉钉应用 AppKey.</param>
    /// <param name="args">长连接推送参数.</param>
    public void Forward(Guid dingTalkAppId, string appKey, MessageEventHanderArgs args)
    {
        Ack(args);

        var headers = args.Headers;
        if (headers == null || string.IsNullOrEmpty(headers.MessageId))
        {
            _logger.LogWarning("钉钉推送缺少 messageId，丢弃，dingTalkAppId={DingTalkAppId}.", dingTalkAppId);
            return;
        }

        if (!_deduplicator.TryBegin(headers.MessageId))
        {
            _logger.LogDebug("钉钉推送重复投递，忽略，dingTalkAppId={DingTalkAppId} messageId={MessageId}.", dingTalkAppId, headers.MessageId);
            return;
        }

        _ = ProcessAsync(
            dingTalkAppId,
            appKey,
            headers.Topic ?? string.Empty,
            headers.MessageId,
            headers.Time.ToString(),
            args.Data ?? string.Empty);
    }

    private static void Ack(MessageEventHanderArgs args)
    {
        try
        {
            // CALLBACK 推送回复 {"response":{"status":"success"}} 信封，避免钉钉因未收到回复而重发；
            // 在收帧线程上同步发送，与 SDK 的系统消息回复天然串行，避免对同一 WebSocket 并发写
            var messageId = args.Headers?.MessageId ?? string.Empty;
            var data = DingTalkStreamUtilities.CreateReply_Callback_MessageData("{\"status\":\"success\"}");
            args.Reply(DingTalkStreamUtilities.CreateReplyMessage(messageId, data).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // ack 失败只影响钉钉侧重发，重发由去重器兜底
        }
    }

    private async Task ProcessAsync(
        Guid dingTalkAppId,
        string appKey,
        string topic,
        string messageId,
        string time,
        string payload)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            // 推送到达时应用可能已被禁用或删除
            var appExists = await databaseContext.DingTalkApps
                .AnyAsync(x => x.Id == dingTalkAppId && !x.IsDisable, CancellationToken.None);

            if (!appExists)
            {
                return;
            }

            // 机器人消息回调携带 chatbotUserId（机器人的 dingtalk id，加密串），回填用于生成「拉起机器人单聊」的 applink；
            // 平台无按 AppKey 拉起机器人会话的稳定链接，只能在首次消息后学习，已回填且未变化时不产生写库
            var chatbotUserId = TryGetChatbotUserId(payload);
            if (!string.IsNullOrEmpty(chatbotUserId))
            {
                await databaseContext.DingTalkApps
                    .Where(x => x.Id == dingTalkAppId && x.ChatbotUserId != chatbotUserId)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.ChatbotUserId, chatbotUserId), CancellationToken.None);
            }

            // 机器人消息独有会话 webhook，其它 Topic 无该字段
            var sessionWebhook = TryGetSessionWebhook(payload);

            // 一个钉钉应用可绑定多个渠道：应用渠道独占一条，后续订阅型渠道可多条
            var bindings = await databaseContext.DingTalkAppBindings
                .Where(x => x.DingTalkAppId == dingTalkAppId)
                .Select(x => new { x.ChannelType, x.ChannelId })
                .ToListAsync(CancellationToken.None);

            if (bindings.Count == 0)
            {
                _logger.LogDebug("钉钉应用未绑定渠道，推送忽略，dingTalkAppId={DingTalkAppId} topic={Topic}.", dingTalkAppId, topic);
                return;
            }

            var allHandlers = scope.ServiceProvider.GetServices<IDingTalkEventHandler>().ToList();

            foreach (var group in bindings.GroupBy(x => (DingTalkChannelType)x.ChannelType))
            {
                var channelType = group.Key;
                var handlers = allHandlers.Where(x => x.ChannelType == channelType).ToList();

                if (handlers.Count == 0)
                {
                    _logger.LogWarning(
                        "渠道 {ChannelType} 没有注册钉钉事件处理器，推送忽略，dingTalkAppId={DingTalkAppId} topic={Topic}.",
                        channelType,
                        dingTalkAppId,
                        topic);
                    continue;
                }

                foreach (var binding in group)
                {
                    var message = new DingTalkEventMessage
                    {
                        DingTalkAppId = dingTalkAppId,
                        AppKey = appKey,
                        Topic = topic,
                        MessageId = messageId,
                        Time = time,
                        Payload = payload,
                        SessionWebhook = sessionWebhook,
                        ChannelType = channelType,
                        ChannelId = binding.ChannelId,
                    };

                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler.HandleAsync(message, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "钉钉事件处理器执行失败，handler={Handler} topic={Topic} messageId={MessageId} channelId={ChannelId}.",
                                handler.GetType().Name,
                                topic,
                                messageId,
                                binding.ChannelId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "钉钉推送转发失败，dingTalkAppId={DingTalkAppId} topic={Topic} messageId={MessageId}.",
                dingTalkAppId,
                topic,
                messageId);
        }
    }

    /// <summary>
    /// 从机器人消息 data 节点提取会话 webhook；解析失败返回空字符串.
    /// </summary>
    private static string TryGetSessionWebhook(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("sessionWebhook", out var node) && node.ValueKind == JsonValueKind.String)
            {
                return node.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 从机器人消息 data 节点提取机器人的 dingtalk id（chatbotUserId，加密串）；非机器人消息或解析失败返回空字符串.
    /// </summary>
    private static string TryGetChatbotUserId(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("chatbotUserId", out var node) && node.ValueKind == JsonValueKind.String)
            {
                return node.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
