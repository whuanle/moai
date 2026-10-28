using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.WeixinWork.Models;

namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信事件转发器：接收解密后的回调推送，按 MsgId 去重后解析推送归属的企业微信应用绑定渠道，
/// 投递给该渠道类型注册的全部 <see cref="IWeixinWorkEventHandler"/>（应用等业务模块实现）.
/// <para>控制器受理（回 success）后转线程池异步处理，不占用回调应答窗口（企业微信 5 秒未收到应答会重发）.</para>
/// </summary>
[InjectOnSingleton]
public sealed partial class WeixinWorkEventForwarder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeixinWorkEventForwarder> _logger;
    private readonly WeixinWorkEventDeduplicator _deduplicator = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkEventForwarder"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="logger">日志.</param>
    public WeixinWorkEventForwarder(IServiceScopeFactory scopeFactory, ILogger<WeixinWorkEventForwarder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 转发解密后的推送：解析报文、去重后转线程池异步处理；重复推送忽略.
    /// </summary>
    /// <param name="weixinWorkAppId">推送归属的企业微信应用记录 id.</param>
    /// <param name="decryptedXml">解密后的明文报文 XML.</param>
    public void Forward(Guid weixinWorkAppId, string decryptedXml)
    {
        var message = WeixinWorkCallbackMessage.Parse(decryptedXml);
        if (message == null)
        {
            _logger.LogWarning("企业微信推送明文解析失败，丢弃，weixinWorkAppId={WeixinWorkAppId}.", weixinWorkAppId);
            return;
        }

        var messageId = message.MsgId;
        if (string.IsNullOrEmpty(messageId))
        {
            // 部分事件推送无 MsgId（如进入会话等），不去重，随机 id 兜底走完整链路
            messageId = Guid.CreateVersion7().ToString("N");
        }
        else if (!_deduplicator.TryBegin(messageId))
        {
            _logger.LogDebug("企业微信推送重复投递，忽略，weixinWorkAppId={WeixinWorkAppId} messageId={MessageId}.", weixinWorkAppId, messageId);
            return;
        }

        _ = ProcessAsync(weixinWorkAppId, message, decryptedXml, messageId);
    }

    private async Task ProcessAsync(Guid weixinWorkAppId, WeixinWorkCallbackMessage message, string payload, string messageId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            // 推送到达时应用可能已被禁用或删除
            var appExists = await databaseContext.WeixinWorkApps
                .AnyAsync(x => x.Id == weixinWorkAppId && !x.IsDisable, CancellationToken.None);

            if (!appExists)
            {
                return;
            }

            // 一个企业微信应用可绑定多个渠道：应用渠道独占一条，后续订阅型渠道可多条
            var bindings = await databaseContext.WeixinWorkAppBindings
                .Where(x => x.WeixinWorkAppId == weixinWorkAppId)
                .Select(x => new { x.ChannelType, x.ChannelId })
                .ToListAsync(CancellationToken.None);

            if (bindings.Count == 0)
            {
                _logger.LogDebug("企业微信应用未绑定渠道，推送忽略，weixinWorkAppId={WeixinWorkAppId} messageId={MessageId}.", weixinWorkAppId, messageId);
                return;
            }

            var allHandlers = scope.ServiceProvider.GetServices<IWeixinWorkEventHandler>().ToList();

            foreach (var group in bindings.GroupBy(x => (WeixinWorkChannelType)x.ChannelType))
            {
                var channelType = group.Key;
                var handlers = allHandlers.Where(x => x.ChannelType == channelType).ToList();

                if (handlers.Count == 0)
                {
                    _logger.LogWarning(
                        "渠道 {ChannelType} 没有注册企业微信事件处理器，推送忽略，weixinWorkAppId={WeixinWorkAppId} messageId={MessageId}.",
                        channelType,
                        weixinWorkAppId,
                        messageId);
                    continue;
                }

                foreach (var binding in group)
                {
                    var eventMessage = new WeixinWorkEventMessage
                    {
                        WeixinWorkAppId = weixinWorkAppId,
                        CorpId = message.ToUserName,
                        AgentId = message.AgentId,
                        MessageId = messageId,
                        FromUserId = message.FromUserName,
                        ChatId = message.ChatId,
                        MessageType = message.MsgType,
                        Content = message.Content,
                        Payload = payload,
                        ChannelType = channelType,
                        ChannelId = binding.ChannelId,
                    };

                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler.HandleAsync(eventMessage, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "企业微信事件处理器执行失败，handler={Handler} messageId={MessageId} channelId={ChannelId}.",
                                handler.GetType().Name,
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
                "企业微信推送转发失败，weixinWorkAppId={WeixinWorkAppId} messageId={MessageId}.",
                weixinWorkAppId,
                messageId);
        }
    }
}
