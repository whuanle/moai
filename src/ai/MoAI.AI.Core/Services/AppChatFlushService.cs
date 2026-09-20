using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI.Compaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;

namespace MoAI.AI.Services;

/// <summary>
/// 会话落库服务：把 Redis 热态消息按压缩策略整理后一次性写入 <c>app_agent_message</c>（压缩后视图），
/// 并回写会话聚合与 PG 冷快照. 被压缩掉的历史按「原文丢弃」处理.
/// </summary>
[InjectOnScoped]
public sealed class AppChatFlushService
{
    private const int TitleLength = 50;

    private readonly AppChatHotStore _hotStore;
    private readonly DatabaseContext _databaseContext;
    private readonly AppCompactionStrategyFactory _compactionStrategyFactory;
    private readonly ILogger<AppChatFlushService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppChatFlushService"/> class.
    /// </summary>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="compactionStrategyFactory">压缩策略工厂.</param>
    /// <param name="logger">日志.</param>
    public AppChatFlushService(
        AppChatHotStore hotStore,
        DatabaseContext databaseContext,
        AppCompactionStrategyFactory compactionStrategyFactory,
        ILogger<AppChatFlushService> logger)
    {
        _hotStore = hotStore;
        _databaseContext = databaseContext;
        _compactionStrategyFactory = compactionStrategyFactory;
        _logger = logger;
    }

    /// <summary>
    /// 落库指定会话.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task FlushAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var records = await _hotStore.GetMessagesAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var session = await _databaseContext.AppAgentSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            return;
        }

        if (records.Count > 0)
        {
            var config = await _databaseContext.AppAgentConfigs.FirstOrDefaultAsync(x => x.AppId == session.AppId, cancellationToken).ConfigureAwait(false);

            // 会话按发布快照的执行参数压缩（正式会话即按该参数运行）；未发布/无快照回退实时草稿
            if (config != null)
            {
                var app = await _databaseContext.Apps.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == session.AppId, cancellationToken).ConfigureAwait(false);
                config = app == null ? config : AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: true);
            }

            var compacted = await CompactAsync(config, records, cancellationToken).ConfigureAwait(false);

            // 物理替换：被压缩掉的行原文丢弃（绕过软删除，避免 (session_id, seq) 唯一索引冲突）
            await _databaseContext.AppAgentMessages
                .Where(x => x.SessionId == sessionId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            var seq = 0;
            foreach (var message in compacted)
            {
                seq++;
                var record = ChatMessageMapper.ToRecord(message, seq);
                _databaseContext.AppAgentMessages.Add(new AppAgentMessageEntity
                {
                    Id = Guid.CreateVersion7(),
                    SessionId = sessionId,
                    Seq = seq,
                    Role = record.Role,
                    Content = record.Content,
                    ToolCalls = record.ToolCalls,
                    ToolCallId = record.ToolCallId,
                    Reasoning = record.Reasoning,
                    CompletionsId = record.CompletionsId,
                });
            }

            if (session.Title == AppAgentConstants.DefaultSessionTitle)
            {
                var firstUser = records.FirstOrDefault(x => string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Content));
                if (firstUser != null)
                {
                    session.Title = Truncate(firstUser.Content, TitleLength);
                }
            }
        }

        var usage = await _hotStore.GetUsageAsync(sessionId, cancellationToken).ConfigureAwait(false);
        session.InputTokens = usage.InputTokens;
        session.OutTokens = usage.OutTokens;
        session.TotalTokens = usage.TotalTokens;
        session.LastMessageTime = DateTimeOffset.Now;

        var snapshot = await _hotStore.GetSessionSnapshotAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(snapshot))
        {
            session.State = snapshot;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Agent 会话 {SessionId} flush 完成，热态消息 {RecordCount} 条.", sessionId, records.Count);
    }

    private async Task<IReadOnlyList<ChatMessage>> CompactAsync(AppAgentConfigEntity? config, IReadOnlyList<AppAgentMessageRecord> records, CancellationToken cancellationToken)
    {
        var messages = records.Select(ChatMessageMapper.ToChatMessage).ToList();
        try
        {
            var strategy = await _compactionStrategyFactory.BuildAsync(config, cancellationToken).ConfigureAwait(false);
            var compacted = await CompactionProvider.CompactAsync(strategy, messages, _logger, cancellationToken).ConfigureAwait(false);
            return compacted.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "会话压缩失败，退回全量消息落库.");
            return messages;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
