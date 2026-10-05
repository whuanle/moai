using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI.Compaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.AIChannel.Services;
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

    private const string TitlePrompt = "请根据用户提问提炼一个简洁的对话标题：概括提问主题，不超过20个字，直接输出标题本身，不要引号、句号或任何前缀与解释。";

    // 附件标记块（文档提取文本/图片链接）不属于提问语义，提炼标题前剔除
    private static readonly Regex AttachmentBlockRegex = new(
        @"<moai-attachment\b[^>]*>[\s\S]*?</moai-attachment>",
        RegexOptions.Compiled);

    private static readonly TimeSpan TitleGenerationTimeout = TimeSpan.FromSeconds(12);

    private readonly AppChatHotStore _hotStore;
    private readonly DatabaseContext _databaseContext;
    private readonly AppCompactionStrategyFactory _compactionStrategyFactory;
    private readonly IAiChatCompletionService _aiChatCompletionService;
    private readonly IAiModelResolver _aiModelResolver;
    private readonly ILogger<AppChatFlushService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppChatFlushService"/> class.
    /// </summary>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="compactionStrategyFactory">压缩策略工厂.</param>
    /// <param name="aiChatCompletionService">一次性对话服务.</param>
    /// <param name="aiModelResolver">模型解析服务.</param>
    /// <param name="logger">日志.</param>
    public AppChatFlushService(
        AppChatHotStore hotStore,
        DatabaseContext databaseContext,
        AppCompactionStrategyFactory compactionStrategyFactory,
        IAiChatCompletionService aiChatCompletionService,
        IAiModelResolver aiModelResolver,
        ILogger<AppChatFlushService> logger)
    {
        _hotStore = hotStore;
        _databaseContext = databaseContext;
        _compactionStrategyFactory = compactionStrategyFactory;
        _aiChatCompletionService = aiChatCompletionService;
        _aiModelResolver = aiModelResolver;
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
                    session.Title = await GenerateTitleAsync(session, config, firstUser.Content, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// 生成会话标题：优先用应用绑定的对话模型从用户首条提问提炼简洁标题；
    /// 应用无模型/模型不可用/生成失败或输出为空时回退为提问原文截断，保证落库不因标题生成而中断.
    /// </summary>
    private async Task<string> GenerateTitleAsync(AppAgentSessionEntity session, AppAgentConfigEntity? config, string question, CancellationToken cancellationToken)
    {
        var fallback = Truncate(question, TitleLength);
        if (config == null || config.ModelId == Guid.Empty)
        {
            return fallback;
        }

        var promptQuestion = StripAttachmentBlocks(question);
        if (string.IsNullOrWhiteSpace(promptQuestion))
        {
            return fallback;
        }

        try
        {
            using var titleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            titleCts.CancelAfter(TitleGenerationTimeout);

            var resolved = await _aiModelResolver.ResolveByIdAsync(config.ModelId, session.TeamId, titleCts.Token).ConfigureAwait(false);
            if (resolved == null)
            {
                return fallback;
            }

            var answer = await _aiChatCompletionService.CompleteTextAsync(
                resolved.Value.Model,
                resolved.Value.Channel,
                $"{TitlePrompt}\n用户提问：{Truncate(promptQuestion, 500)}",
                new AiChatCompletionOptions
                {
                    // 标题提炼无需思维链：禁用思考避免推理型模型把输出预算耗在 reasoning 上导致正文为空
                    DisableThinking = true,
                    MaxOutputTokens = 100,
                    Temperature = 0.3f,
                },
                titleCts.Token).ConfigureAwait(false);

            var title = CleanTitle(answer);
            return string.IsNullOrWhiteSpace(title) ? fallback : Truncate(title, TitleLength);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "会话 {SessionId} AI 生成标题失败，回退为提问原文截断.", session.Id);
            return fallback;
        }
    }

    private static string StripAttachmentBlocks(string content)
    {
        return AttachmentBlockRegex.Replace(content, string.Empty).Trim();
    }

    private static string CleanTitle(string title)
    {
        var line = title.Split('\r', '\n').FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
        foreach (var prefix in new[] { "标题：", "标题:", "标题 " })
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                line = line[prefix.Length..].Trim();
            }
        }

        return line.Trim('"', '“', '”', '「', '」', '『', '』', '\'', '‘', '’');
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
