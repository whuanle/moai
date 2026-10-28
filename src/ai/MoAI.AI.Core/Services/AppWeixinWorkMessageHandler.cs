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
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.WeixinWork.Models;
using MoAI.WeixinWork.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// 应用渠道的企业微信消息处理器：接收自建应用回调消息（单聊/群聊），按「企业微信应用 + 会话（chatId 或成员 userid）」
/// 定位或创建应用 Agent 会话（与 AG-UI 同一套会话管线：热态快照 + 历史提供者 + 落库压缩），运行 Agent 后
/// 通过应用消息接口把回复发回成员/群聊.
/// <para>单例注册：内部按会话 key 加锁串行执行，避免同一会话并发写历史错序.</para>
/// </summary>
public sealed class AppWeixinWorkMessageHandler : IWeixinWorkEventHandler
{
    private const int ReplyMaxLength = 3000;
    private static readonly TimeSpan ChatSessionTtl = TimeSpan.FromDays(30);
    private static readonly Regex MentionPrefixRegex = new("^@[^\\s]+\\s*", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWeixinWorkApiClient _weixinWorkApiClient;
    private readonly ILogger<AppWeixinWorkMessageHandler> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _chatLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppWeixinWorkMessageHandler"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="weixinWorkApiClient">企业微信应用消息客户端.</param>
    /// <param name="logger">日志.</param>
    public AppWeixinWorkMessageHandler(IServiceScopeFactory scopeFactory, IWeixinWorkApiClient weixinWorkApiClient, ILogger<AppWeixinWorkMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _weixinWorkApiClient = weixinWorkApiClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public WeixinWorkChannelType ChannelType => WeixinWorkChannelType.App;

    /// <inheritdoc/>
    public async Task HandleAsync(WeixinWorkEventMessage message, CancellationToken cancellationToken)
    {
        // 当前仅支持文本消息；event（进入会话等）与其它类型忽略
        if (!string.Equals(message.MessageType, "text", StringComparison.Ordinal))
        {
            return;
        }

        var text = ExtractPlainText(message.Content);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var gate = _chatLocks.GetOrAdd(ChatLockKey(message.WeixinWorkAppId, message.ChatId, message.FromUserId), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var reply = await ProcessAsync(message, text, cancellationToken);
            if (!string.IsNullOrWhiteSpace(reply))
            {
                await _weixinWorkApiClient.SendTextMessageAsync(
                    message.WeixinWorkAppId,
                    message.FromUserId,
                    message.ChatId,
                    Truncate(reply, ReplyMaxLength),
                    cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> ProcessAsync(WeixinWorkEventMessage message, string text, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.ChannelId, out var appId))
        {
            _logger.LogWarning("企业微信绑定渠道 id 非法，weixinWorkAppId={WeixinWorkAppId} channelId={ChannelId}.", message.WeixinWorkAppId, message.ChannelId);
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
            _logger.LogInformation("企业微信消息忽略（应用不可用），appId={AppId} messageId={MessageId}.", appId, message.MessageId);
            return string.Empty;
        }

        var hotStore = scope.ServiceProvider.GetRequiredService<AppChatHotStore>();
        var conversationId = ResolveConversationId(message);
        var sessionId = await ResolveSessionIdAsync(scope, message.WeixinWorkAppId, conversationId, app.TeamId, appId, cancellationToken);

        try
        {
            var factory = scope.ServiceProvider.GetRequiredService<AppAgentFactory>();

            // 企业微信成员不映射内部用户：userId=0，用量计入团队/应用维度；企业微信会话暂不支持专家提示词
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
            _logger.LogWarning("企业微信对话业务失败，appId={AppId} messageId={MessageId} msg={Msg}.", appId, message.MessageId, ex.Message);
            return ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "企业微信对话运行失败，appId={AppId} messageId={MessageId}.", appId, message.MessageId);
            return "应用回复失败，请稍后再试.";
        }
    }

    /// <summary>
    /// 定位或创建「企业微信应用 + 会话」对应的 Agent 会话；会话键群聊取 chatId、单聊取成员 userid，
    /// 映射存 Redis（30 天滑动），映射丢失时自愈为新建会话（旧会话成为孤儿但不再被使用）.
    /// </summary>
    private async Task<Guid> ResolveSessionIdAsync(IServiceScope scope, Guid weixinWorkAppId, string conversationId, int teamId, Guid appId, CancellationToken cancellationToken)
    {
        var redisDatabase = scope.ServiceProvider.GetRequiredService<IRedisDatabase>();
        var key = ChatSessionKey(weixinWorkAppId, conversationId);

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
        _logger.LogInformation("企业微信会话已创建，weixinWorkAppId={WeixinWorkAppId} conversationId={ConversationId} sessionId={SessionId}.", weixinWorkAppId, conversationId, session.Id);
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
    /// 提取企业微信文本消息内容并去掉群聊 @应用 的前缀（content 里可能带 "@应用名 " 前缀）.
    /// </summary>
    private static string ExtractPlainText(string content)
    {
        return MentionPrefixRegex.Replace(content ?? string.Empty, string.Empty).Trim();
    }

    /// <summary>
    /// 会话键：群聊取 chatId（稳定），单聊取成员 userid.
    /// </summary>
    private static string ResolveConversationId(WeixinWorkEventMessage message)
    {
        return string.IsNullOrEmpty(message.ChatId) ? message.FromUserId : message.ChatId;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string ChatLockKey(Guid weixinWorkAppId, string chatId, string fromUserId) => $"{weixinWorkAppId:N}:{ResolveChatId(chatId, fromUserId)}";

    private static string ChatSessionKey(Guid weixinWorkAppId, string conversationId) => $"weixinwork:chat:{weixinWorkAppId:N}:{conversationId}";

    private static string ResolveChatId(string chatId, string fromUserId) => string.IsNullOrEmpty(chatId) ? fromUserId : chatId;
}
