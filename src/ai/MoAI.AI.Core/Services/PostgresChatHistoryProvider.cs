using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using MoAI.AI.Models;
using MoAI.Database;

namespace MoAI.AI.Services;

/// <summary>
/// Agent 会话历史提供者：运行期间读写 Redis 热态，未命中回表 <c>app_agent_message</c> 并回填.
/// <para>请求消息仅落 External 来源，避免把 RAG/上下文注入与历史来源消息重复落库.</para>
/// </summary>
public sealed class PostgresChatHistoryProvider : ChatHistoryProvider
{
    private readonly AppChatHotStore _hotStore;
    private readonly DatabaseContext _databaseContext;
    private readonly Guid _sessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresChatHistoryProvider"/> class.
    /// </summary>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="sessionId">会话 id.</param>
    public PostgresChatHistoryProvider(AppChatHotStore hotStore, DatabaseContext databaseContext, Guid sessionId)
        : base(
            provideOutputMessageFilter: null,
            storeInputRequestMessageFilter: messages => messages.Where(m => m.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External),
            storeInputResponseMessageFilter: null)
    {
        _hotStore = hotStore;
        _databaseContext = databaseContext;
        _sessionId = sessionId;
    }

    /// <inheritdoc/>
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var records = await _hotStore.GetMessagesAsync(_sessionId, cancellationToken);
        if (records.Count == 0)
        {
            records = await LoadFromDatabaseAsync(cancellationToken);
            if (records.Count > 0)
            {
                await _hotStore.AppendMessagesAsync(_sessionId, records, cancellationToken);
            }
        }

        return records.Select(ChatMessageMapper.ToChatMessage).ToList();
    }

    /// <inheritdoc/>
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        var all = context.RequestMessages.Concat(context.ResponseMessages ?? []);
        var existing = await _hotStore.GetMessagesAsync(_sessionId, cancellationToken);
        var seq = existing.Count == 0 ? 0 : existing.Max(x => x.Seq);

        var records = new List<AppAgentMessageRecord>();
        foreach (var message in all)
        {
            seq++;
            records.Add(ChatMessageMapper.ToRecord(message, seq));
        }

        await _hotStore.AppendMessagesAsync(_sessionId, records, cancellationToken);
    }

    private async Task<List<AppAgentMessageRecord>> LoadFromDatabaseAsync(CancellationToken cancellationToken)
    {
        return await _databaseContext.AppAgentMessages
            .Where(x => x.SessionId == _sessionId)
            .OrderBy(x => x.Seq)
            .Select(x => new AppAgentMessageRecord
            {
                Seq = x.Seq,
                Role = x.Role,
                Content = x.Content,
                ToolCalls = x.ToolCalls,
                ToolCallId = x.ToolCallId,
                Reasoning = x.Reasoning,
                CompletionsId = x.CompletionsId,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);
    }
}
