using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Feishu.Models;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// 应用渠道的飞书消息处理器：接收 im.message.receive_v1（群聊/私聊），按「飞书应用 + chat_id」定位或创建
/// 应用 Agent 会话（与 AG-UI 同一套会话管线：热态快照 + 历史提供者 + 落库压缩），运行 Agent 后把回复发回原会话.
/// <para>单例注册：内部按 chat_id 加锁串行执行，避免同一会话并发写历史错序.</para>
/// </summary>
public sealed class AppFeishuMessageHandler : IFeishuEventHandler
{
    private const string MessageReceiveEventType = "im.message.receive_v1";
    private const string ReceiveIdTypeChatId = "chat_id";
    private const int ReplyMaxLength = 3000;
    private static readonly TimeSpan ChatSessionTtl = TimeSpan.FromDays(30);
    private static readonly Regex MentionPlaceholderRegex = new("@_user_\\d+", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFeishuApiClient _feishuApiClient;
    private readonly ILogger<AppFeishuMessageHandler> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _chatLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppFeishuMessageHandler"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="feishuApiClient">飞书开放平台客户端.</param>
    /// <param name="logger">日志.</param>
    public AppFeishuMessageHandler(IServiceScopeFactory scopeFactory, IFeishuApiClient feishuApiClient, ILogger<AppFeishuMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _feishuApiClient = feishuApiClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public FeishuChannelType ChannelType => FeishuChannelType.App;

    /// <inheritdoc/>
    public async Task HandleAsync(FeishuEventMessage message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.EventType, MessageReceiveEventType, StringComparison.Ordinal))
        {
            return;
        }

        var received = FeishuReceivedMessage.TryParse(message.Payload);
        if (received == null)
        {
            _logger.LogWarning("飞书消息事件解析失败，appId={AppId} eventId={EventId}.", message.AppId, message.EventId);
            return;
        }

        // 只处理真人用户消息，忽略机器人消息避免回路；当前仅支持文本消息
        if (!string.Equals(received.SenderType, "user", StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(received.MessageType, "text", StringComparison.Ordinal))
        {
            return;
        }

        var text = ExtractPlainText(received.Content);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var gate = _chatLocks.GetOrAdd(ChatLockKey(message.FeishuAppId, received.ChatId), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var reply = await ProcessAsync(message, received, text, cancellationToken);
            if (!string.IsNullOrWhiteSpace(reply))
            {
                await _feishuApiClient.SendTextMessageAsync(
                    message.FeishuAppId,
                    ReceiveIdTypeChatId,
                    received.ChatId,
                    Truncate(reply, ReplyMaxLength),
                    cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> ProcessAsync(FeishuEventMessage message, FeishuReceivedMessage received, string text, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.ChannelId, out var appId))
        {
            _logger.LogWarning("飞书绑定渠道 id 非法，feishuAppId={FeishuAppId} channelId={ChannelId}.", message.FeishuAppId, message.ChannelId);
            return string.Empty;
        }

        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var app = await databaseContext.Apps
            .Where(x => x.Id == appId)
            .Select(x => new { x.TeamId, x.AppType, x.PublishStatus, x.IsDisable, x.IsExternal })
            .FirstOrDefaultAsync(cancellationToken);

        // 仅已发布的内部 Agent 应用对外服务；未发布/禁用/外部应用静默忽略
        if (app == null || app.IsDisable || app.IsExternal || app.AppType != (int)AppType.Agent || app.PublishStatus != 1)
        {
            _logger.LogInformation("飞书消息忽略（应用不可用），appId={AppId} eventId={EventId}.", appId, message.EventId);
            return string.Empty;
        }

        var hotStore = scope.ServiceProvider.GetRequiredService<AppChatHotStore>();
        var sessionId = await ResolveSessionIdAsync(scope, message.FeishuAppId, received.ChatId, app.TeamId, appId, cancellationToken);

        try
        {
            var factory = scope.ServiceProvider.GetRequiredService<AppAgentFactory>();

            // 飞书用户不映射内部用户：userId=0，用量计入团队/应用维度
            var agent = await factory.CreateAsync(appId, app.TeamId, userId: 0, sessionId, isDebug: false, cancellationToken);
            var session = await LoadSessionAsync(agent, hotStore, sessionId, cancellationToken);

            var response = await agent.RunAsync([new ChatMessage(ChatRole.User, text)], session, cancellationToken: cancellationToken);
            var reply = response.Messages?.LastOrDefault(x => x.Role == ChatRole.Assistant)?.Text ?? string.Empty;

            await SaveSessionAsync(scope, agent, hotStore, sessionId, session, cancellationToken);
            return reply;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException ex)
        {
            // 配置类问题（如未配置模型）对用户可见
            _logger.LogWarning("飞书对话业务失败，appId={AppId} eventId={EventId} msg={Msg}.", appId, message.EventId, ex.Message);
            return ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "飞书对话运行失败，appId={AppId} eventId={EventId}.", appId, message.EventId);
            return "应用回复失败，请稍后再试.";
        }
    }

    /// <summary>
    /// 定位或创建「飞书应用 + chat_id」对应的 Agent 会话；映射存 Redis（30 天滑动），
    /// 映射丢失时自愈为新建会话（旧会话成为孤儿但不再被使用）.
    /// </summary>
    private async Task<Guid> ResolveSessionIdAsync(IServiceScope scope, Guid feishuAppId, string chatId, int teamId, Guid appId, CancellationToken cancellationToken)
    {
        var redisDatabase = scope.ServiceProvider.GetRequiredService<IRedisDatabase>();
        var key = ChatSessionKey(feishuAppId, chatId);

        var mapped = await redisDatabase.Database.StringGetAsync(key);
        if (mapped.HasValue && Guid.TryParse(mapped.ToString(), out var sessionId))
        {
            await redisDatabase.Database.KeyExpireAsync(key, ChatSessionTtl);
            return sessionId;
        }

        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = teamId,
            AppId = appId,
            Title = AppAgentConstants.DefaultSessionTitle,
            UserType = (int)UserType.External,
            LastMessageTime = DateTimeOffset.Now,
        };
        databaseContext.AppAgentSessions.Add(session);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await redisDatabase.Database.StringSetAsync(key, session.Id.ToString(), ChatSessionTtl);
        _logger.LogInformation("飞书会话已创建，feishuAppId={FeishuAppId} chatId={ChatId} sessionId={SessionId}.", feishuAppId, chatId, session.Id);
        return session.Id;
    }

    /// <summary>
    /// 加载会话热态快照（与 <see cref="AppAgentSessionStore.GetSessionAsync"/> 同一套加载逻辑）.
    /// </summary>
    private static async Task<AgentSession> LoadSessionAsync(AIAgent agent, AppChatHotStore hotStore, Guid sessionId, CancellationToken cancellationToken)
    {
        AgentSession session;
        var snapshot = await hotStore.GetSessionSnapshotAsync(sessionId, cancellationToken);
        if (!string.IsNullOrEmpty(snapshot))
        {
            using var document = JsonDocument.Parse(snapshot);
            session = await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
        }
        else
        {
            session = await agent.CreateSessionAsync(cancellationToken);
        }

        session.StateBag.SetValue(AppAgentConstants.SessionIdStateKey, sessionId.ToString());
        return session;
    }

    /// <summary>
    /// 保存会话热态快照并触发落库（压缩后消息 + 聚合回写 + 冷快照）.
    /// </summary>
    private static async Task SaveSessionAsync(IServiceScope scope, AIAgent agent, AppChatHotStore hotStore, Guid sessionId, AgentSession session, CancellationToken cancellationToken)
    {
        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        await hotStore.SetSessionSnapshotAsync(sessionId, serialized.GetRawText(), cancellationToken);

        var flushService = scope.ServiceProvider.GetRequiredService<AppChatFlushService>();
        await flushService.FlushAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// 提取飞书文本消息内容并去掉 @占位符（群聊 @机器人 时正文里是 @_user_N）.
    /// </summary>
    private static string ExtractPlainText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("text", out var textNode) || textNode.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return MentionPlaceholderRegex.Replace(textNode.GetString() ?? string.Empty, string.Empty).Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string ChatLockKey(Guid feishuAppId, string chatId) => $"{feishuAppId:N}:{chatId}";

    private static string ChatSessionKey(Guid feishuAppId, string chatId) => $"feishu:chat:{feishuAppId:N}:{chatId}";

    /// <summary>
    /// im.message.receive_v1 事件体的最小解析视图.
    /// </summary>
    private sealed class FeishuReceivedMessage
    {
        /// <summary>
        /// 发送者类型：user/app.
        /// </summary>
        public string SenderType { get; init; } = string.Empty;

        /// <summary>
        /// 会话 id（oc_xxx）.
        /// </summary>
        public string ChatId { get; init; } = string.Empty;

        /// <summary>
        /// 消息类型：text/image/…
        /// </summary>
        public string MessageType { get; init; } = string.Empty;

        /// <summary>
        /// 消息内容（text 类型为 {"text":"..."} JSON 字符串）.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        public static FeishuReceivedMessage? TryParse(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (!root.TryGetProperty("message", out var messageNode))
                {
                    return null;
                }

                string GetString(JsonElement node, string name)
                    => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

                var senderType = string.Empty;
                if (root.TryGetProperty("sender", out var senderNode))
                {
                    senderType = GetString(senderNode, "sender_type");
                }

                return new FeishuReceivedMessage
                {
                    SenderType = senderType,
                    ChatId = GetString(messageNode, "chat_id"),
                    MessageType = GetString(messageNode, "message_type"),
                    Content = GetString(messageNode, "content"),
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
