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
using MoAI.Database.Aggregates;

namespace MoAI.AI.Services;

/// <summary>
/// Agent 会话历史提供者：运行期间读写 Redis 热态，未命中回表 <c>app_agent_message</c> 并回填.
/// <para>请求消息仅落 External 来源，避免把 RAG/上下文注入与历史来源消息重复落库.</para>
/// <para>携带脱敏策略时，落库前对工具调用参数与模型正文记录脱敏（工具结果脱敏由函数调用中间件在源头完成）.</para>
/// </summary>
public sealed class PostgresChatHistoryProvider : ChatHistoryProvider
{
    private readonly AppChatHotStore _hotStore;
    private readonly DatabaseContext _databaseContext;
    private readonly Guid _sessionId;
    private readonly AppSecurityPolicy? _securityPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresChatHistoryProvider"/> class.
    /// </summary>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="securityPolicy">内容脱敏策略（可为 null，表示未启用）.</param>
    public PostgresChatHistoryProvider(AppChatHotStore hotStore, DatabaseContext databaseContext, Guid sessionId, AppSecurityPolicy? securityPolicy = null)
        : base(
            provideOutputMessageFilter: null,
            storeInputRequestMessageFilter: messages => messages.Where(m => m.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External),
            storeInputResponseMessageFilter: null)
    {
        _hotStore = hotStore;
        _databaseContext = databaseContext;
        _sessionId = sessionId;
        _securityPolicy = securityPolicy is { IsActive: true } ? securityPolicy : null;
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

        // 落库前脱敏：工具调用参数记录 + 模型正文/思考记录（工具消息的 Content 为 TextContent 聚合、不含函数结果，
        // 工具结果已在函数调用中间件源头脱敏；此处兜底覆盖参数与正文）
        if (_securityPolicy != null)
        {
            foreach (var record in records)
            {
                if (!string.IsNullOrEmpty(record.ToolCalls) && record.ToolCalls != "[]")
                {
                    record.ToolCalls = _securityPolicy.MaskToolArgsText(record.ToolCalls);
                }

                if (string.Equals(record.Role, ChatRole.Assistant.Value, StringComparison.OrdinalIgnoreCase))
                {
                    record.Content = _securityPolicy.MaskModelText(record.Content);
                    record.Reasoning = _securityPolicy.MaskModelText(record.Reasoning);
                }
            }
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
