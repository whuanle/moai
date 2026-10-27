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
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.DingTalk.Models;
using MoAI.DingTalk.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// 应用渠道的钉钉消息处理器：接收机器人消息推送（群聊/私聊），按「钉钉应用 + conversationId」定位或创建
/// 应用 Agent 会话（与 AG-UI 同一套会话管线：热态快照 + 历史提供者 + 落库压缩），运行 Agent 后把回复发回原会话.
/// <para>单例注册：内部按 conversationId 加锁串行执行，避免同一会话并发写历史错序.</para>
/// </summary>
public sealed class AppDingTalkMessageHandler : IDingTalkEventHandler
{
    private const int ReplyMaxLength = 3000;
    private static readonly TimeSpan ChatSessionTtl = TimeSpan.FromDays(30);
    private static readonly Regex MentionPrefixRegex = new("^@[^\\s]+\\s*", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDingTalkApiClient _dingTalkApiClient;
    private readonly ILogger<AppDingTalkMessageHandler> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _chatLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDingTalkMessageHandler"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="dingTalkApiClient">钉钉机器人消息客户端.</param>
    /// <param name="logger">日志.</param>
    public AppDingTalkMessageHandler(IServiceScopeFactory scopeFactory, IDingTalkApiClient dingTalkApiClient, ILogger<AppDingTalkMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _dingTalkApiClient = dingTalkApiClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DingTalkChannelType ChannelType => DingTalkChannelType.App;

    /// <inheritdoc/>
    public async Task HandleAsync(DingTalkEventMessage message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.Topic, DingTalkEventMessage.RobotMessageTopic, StringComparison.Ordinal))
        {
            return;
        }

        var received = DingTalkReceivedMessage.TryParse(message.Payload);
        if (received == null)
        {
            _logger.LogWarning("钉钉消息推送解析失败，appKey={AppKey} messageId={MessageId}.", message.AppKey, message.MessageId);
            return;
        }

        // 当前仅支持文本消息
        if (!string.Equals(received.MessageType, "text", StringComparison.Ordinal))
        {
            return;
        }

        var text = ExtractPlainText(received.Content);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message.SessionWebhook))
        {
            _logger.LogWarning("钉钉消息缺少会话 webhook，无法回复，appKey={AppKey} messageId={MessageId}.", message.AppKey, message.MessageId);
            return;
        }

        var gate = _chatLocks.GetOrAdd(ChatLockKey(message.DingTalkAppId, received.ConversationId), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var reply = await ProcessAsync(message, received, text, cancellationToken);
            if (!string.IsNullOrWhiteSpace(reply))
            {
                await _dingTalkApiClient.SendTextMessageAsync(
                    message.SessionWebhook,
                    Truncate(reply, ReplyMaxLength),
                    cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> ProcessAsync(DingTalkEventMessage message, DingTalkReceivedMessage received, string text, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.ChannelId, out var appId))
        {
            _logger.LogWarning("钉钉绑定渠道 id 非法，dingTalkAppId={DingTalkAppId} channelId={ChannelId}.", message.DingTalkAppId, message.ChannelId);
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
            _logger.LogInformation("钉钉消息忽略（应用不可用），appId={AppId} messageId={MessageId}.", appId, message.MessageId);
            return string.Empty;
        }

        var hotStore = scope.ServiceProvider.GetRequiredService<AppChatHotStore>();
        var sessionId = await ResolveSessionIdAsync(scope, message.DingTalkAppId, received.ConversationId, app.TeamId, appId, cancellationToken);

        try
        {
            var factory = scope.ServiceProvider.GetRequiredService<AppAgentFactory>();

            // 钉钉用户不映射内部用户：userId=0，用量计入团队/应用维度；钉钉会话暂不支持专家提示词
            var agent = await factory.CreateAsync(appId, app.TeamId, userId: 0, sessionId, isDebug: false, promptId: 0, cancellationToken);
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
            _logger.LogWarning("钉钉对话业务失败，appId={AppId} messageId={MessageId} msg={Msg}.", appId, message.MessageId, ex.Message);
            return ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉对话运行失败，appId={AppId} messageId={MessageId}.", appId, message.MessageId);
            return "应用回复失败，请稍后再试.";
        }
    }

    /// <summary>
    /// 定位或创建「钉钉应用 + conversationId」对应的 Agent 会话；映射存 Redis（30 天滑动），
    /// 映射丢失时自愈为新建会话（旧会话成为孤儿但不再被使用）.
    /// </summary>
    private async Task<Guid> ResolveSessionIdAsync(IServiceScope scope, Guid dingTalkAppId, string conversationId, int teamId, Guid appId, CancellationToken cancellationToken)
    {
        var redisDatabase = scope.ServiceProvider.GetRequiredService<IRedisDatabase>();
        var key = ChatSessionKey(dingTalkAppId, conversationId);

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

        // 使用次数 +1（Redis 计数器，删除会话不回退）
        await scope.ServiceProvider.GetRequiredService<IAppUsageCounter>().IncrementAsync(appId, cancellationToken);

        await redisDatabase.Database.StringSetAsync(key, session.Id.ToString(), ChatSessionTtl);
        _logger.LogInformation("钉钉会话已创建，dingTalkAppId={DingTalkAppId} conversationId={ConversationId} sessionId={SessionId}.", dingTalkAppId, conversationId, session.Id);
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
    /// 提取钉钉文本消息内容并去掉群聊 @机器人 的前缀（content 里可能带 "@机器人名 " 前缀）.
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
            if (!document.RootElement.TryGetProperty("content", out var textNode) || textNode.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return MentionPrefixRegex.Replace(textNode.GetString() ?? string.Empty, string.Empty).Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string ChatLockKey(Guid dingTalkAppId, string conversationId) => $"{dingTalkAppId:N}:{conversationId}";

    private static string ChatSessionKey(Guid dingTalkAppId, string conversationId) => $"dingtalk:chat:{dingTalkAppId:N}:{conversationId}";

    /// <summary>
    /// 机器人消息推送体的最小解析视图.
    /// </summary>
    private sealed class DingTalkReceivedMessage
    {
        /// <summary>
        /// 会话 id（单聊/群聊均稳定）.
        /// </summary>
        public string ConversationId { get; init; } = string.Empty;

        /// <summary>
        /// 消息类型：text/image/…
        /// </summary>
        public string MessageType { get; init; } = string.Empty;

        /// <summary>
        /// 消息内容（text 类型为 {"content":"..."} JSON 字符串）.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        public static DingTalkReceivedMessage? TryParse(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                string GetString(JsonElement node, params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            return value.GetString() ?? string.Empty;
                        }
                    }

                    return string.Empty;
                }

                return new DingTalkReceivedMessage
                {
                    ConversationId = GetString(root, "conversationId"),

                    // 钉钉线上字段为全小写 msgtype（官方 SDK 的 C# 属性名 MsgType 是驼峰，勿混淆）
                    MessageType = GetString(root, "msgtype", "msgType"),
                    Content = root.TryGetProperty("text", out var textNode) && textNode.ValueKind == JsonValueKind.Object
                        ? textNode.GetRawText()
                        : string.Empty,
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
