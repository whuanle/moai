using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Memory.Models;
using MoAI.Memory.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Memory.Services;

/// <summary>
/// <inheritdoc cref="IMemoryService"/>
/// 记忆向量本体存于 __agent_mem_{appId} 动态集合，<see cref="AppMemoryShardEntity"/> 登记行数支撑跨应用发现；
/// 写入走「去重合并 + 容量淘汰」，自动提取走 mem0 式两段 LLM（事实抽取 → 合并决策），以 Redis 会话锁串行化.
/// </summary>
[InjectOnScoped]
public sealed class MemoryService : IMemoryService
{
    /// <summary>
    /// Redis 会话提取锁 TTL（分钟）.
    /// </summary>
    private const int LockTtlMinutes = 5;

    /// <summary>
    /// 单条记忆内容长度上限（字符）.
    /// </summary>
    private const int MaxMemoryContentLength = 500;

    /// <summary>
    /// 提取用对话片段总长度上限（字符）.
    /// </summary>
    private const int MaxConversationLength = 8000;

    /// <summary>
    /// 单条对话消息参与提取的长度上限（字符）.
    /// </summary>
    private const int MaxMessageLength = 1500;

    private const int ListMaxRecords = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryVectorStore _vectorStore;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IAiChatCompletionService _aiChatCompletionService;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<MemoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryService"/> class.
    /// </summary>
    public MemoryService(
        DatabaseContext databaseContext,
        IMemoryVectorStore vectorStore,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        IAiChatCompletionService aiChatCompletionService,
        IRedisDatabase redisDatabase,
        ILogger<MemoryService> logger)
    {
        _databaseContext = databaseContext;
        _vectorStore = vectorStore;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _aiChatCompletionService = aiChatCompletionService;
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MemoryResolution?> ResolveAsync(Guid appId, long userId, CancellationToken cancellationToken = default)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == appId)
            .Select(x => new { x.Id, x.TeamId, x.AppType, x.PublishStatus, x.IsExternal })
            .FirstOrDefaultAsync(cancellationToken);
        if (app == null || app.IsExternal)
        {
            return null;
        }

        var settings = await ResolveSettingsAsync(app.Id, app.TeamId, app.AppType, app.PublishStatus, cancellationToken);
        if (settings is not { Enabled: true })
        {
            return null;
        }

        var userMode = await _databaseContext.AppUserConfigs.AsNoTracking()
            .Where(x => x.AppId == appId && x.UserId == userId)
            .Select(x => (int?)x.MemoryMode)
            .FirstOrDefaultAsync(cancellationToken) ?? AgentMemorySettings.UserModeFollowApp;

        return new MemoryResolution
        {
            Settings = settings,
            UserMode = settings.ResolveUserMode(userMode),
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryItem>> SearchAsync(MemorySearchQuery query, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(query.AppId, query.UserId, cancellationToken);
        if (resolution is not { IsEnabled: true })
        {
            return [];
        }

        var settings = resolution.Settings;
        var types = query.Types is { Count: > 0 }
            ? query.Types.Distinct().ToList()
            : AllTypes();

        // 记忆仅本应用内检索（各应用向量模型与维度不同，不做跨应用检索）
        var top = Math.Clamp(query.TopK <= 0 ? settings.RetrievalTopK : query.TopK, 1, AgentMemorySettings.MaxRetrievalTopK);
        var minScore = query.MinScore is > 0 and <= 1 ? query.MinScore : settings.MinScore;

        var queryVector = await GenerateQueryVectorAsync(settings.EmbeddingModelId, settings.EmbeddingDimensions, await ResolveTeamIdAsync(query.AppId, cancellationToken), query.QueryText, cancellationToken);
        if (queryVector == null)
        {
            return [];
        }

        var hits = await _vectorStore.SearchAsync(query.AppId, queryVector.Value, top, query.UserId, types, cancellationToken);
        return hits
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(top)
            .Select(x => ToItem(query.AppId, x.Record, x.Score))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<MemoryItem> SaveAsync(MemorySaveRequest request, CancellationToken cancellationToken = default)
    {
        var settings = request.Settings;
        var content = (request.Content ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            throw new InvalidOperationException("记忆内容不能为空.");
        }

        if (content.Length > MaxMemoryContentLength)
        {
            content = content[..MaxMemoryContentLength];
        }

        var shard = await GetShardAsync(request.AppId, request.UserId, cancellationToken);
        var currentCount = shard?.MemoryCount ?? 0;

        // 容量淘汰：超出单用户上限时删除最旧记忆腾出空间
        if (currentCount >= settings.MaxPerUser)
        {
            var need = currentCount - settings.MaxPerUser + 1;
            var all = await _vectorStore.ListUserAsync(request.AppId, request.UserId, ListMaxRecords, cancellationToken);
            var oldest = all.OrderBy(x => x.CreateTimeMs).Take(need).ToList();
            if (oldest.Count > 0)
            {
                await _vectorStore.DeleteAsync(request.AppId, oldest.Select(x => x.Key).ToList(), cancellationToken);
                await UpdateShardAsync(request.AppId, request.TeamId, request.UserId, -oldest.Count, cancellationToken);
                currentCount -= oldest.Count;
            }
        }

        var now = DateTimeOffset.Now;
        var vector = await GenerateVectorAsync(request.AppId, request.TeamId, settings, content, cancellationToken)
            ?? throw new InvalidOperationException("记忆向量化模型不可用.");

        // 去重合并：与既有高相似记忆更新为最新表述
        var hits = await _vectorStore.SearchAsync(request.AppId, vector, 1, request.UserId, null, cancellationToken);
        var duplicate = hits.FirstOrDefault();
        AgentMemoryVectorRecord record;
        if (duplicate.Record != null && duplicate.Score >= AgentMemorySettings.DedupeScoreThreshold)
        {
            record = new AgentMemoryVectorRecord
            {
                Key = duplicate.Record.Key,
                UserId = request.UserId,
                MemoryType = request.MemoryType,
                Content = content,
                SourceSessionId = request.SessionId,
                CreateTimeMs = duplicate.Record.CreateTimeMs,
                UpdateTimeMs = now.ToUnixTimeMilliseconds(),
                Embedding = vector,
            };
            await _vectorStore.UpsertAsync(request.AppId, [record], cancellationToken);
            return ToItem(request.AppId, record, duplicate.Score);
        }

        record = new AgentMemoryVectorRecord
        {
            Key = Guid.CreateVersion7(),
            UserId = request.UserId,
            MemoryType = request.MemoryType,
            Content = content,
            SourceSessionId = request.SessionId,
            CreateTimeMs = now.ToUnixTimeMilliseconds(),
            UpdateTimeMs = now.ToUnixTimeMilliseconds(),
            Embedding = vector,
        };
        await _vectorStore.UpsertAsync(request.AppId, [record], cancellationToken);
        await UpdateShardAsync(request.AppId, request.TeamId, request.UserId, 1, cancellationToken);
        return ToItem(request.AppId, record, 0);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid appId, Guid memoryId, long userId, CancellationToken cancellationToken = default)
    {
        var records = await _vectorStore.ListUserAsync(appId, userId, ListMaxRecords, cancellationToken);
        var target = records.FirstOrDefault(x => x.Key == memoryId);
        if (target == null)
        {
            return false;
        }

        await _vectorStore.DeleteAsync(appId, [memoryId], cancellationToken);
        await UpdateShardAsync(appId, await ResolveTeamIdAsync(appId, cancellationToken), userId, -1, cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> ClearAppUserAsync(Guid appId, long userId, CancellationToken cancellationToken = default)
    {
        var records = await _vectorStore.ListUserAsync(appId, userId, ListMaxRecords, cancellationToken);
        if (records.Count == 0)
        {
            return 0;
        }

        await _vectorStore.DeleteAsync(appId, records.Select(x => x.Key).ToList(), cancellationToken);
        await UpdateShardAsync(appId, await ResolveTeamIdAsync(appId, cancellationToken), userId, -records.Count, cancellationToken);
        return records.Count;
    }

    /// <inheritdoc/>
    public async Task<int> ClearAppAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == appId)
            .Select(x => new { x.Id, x.TeamId, x.AppType, x.PublishStatus })
            .FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            return 0;
        }

        var settings = await ResolveSettingsAsync(app.Id, app.TeamId, app.AppType, app.PublishStatus, cancellationToken);
        if (settings == null)
        {
            return 0;
        }

        await _vectorStore.DeleteCollectionAsync(appId, cancellationToken);
        await _databaseContext.AppMemoryShards
            .Where(x => x.AppId == appId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.MemoryCount, 0)
                .SetProperty(x => x.UpdateTime, DateTimeOffset.Now), cancellationToken);
        return 0;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<MemoryItem> Items, int Total)> ListMineAsync(Guid appId, long userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var records = await _vectorStore.ListUserAsync(appId, userId, ListMaxRecords, cancellationToken);
        var ordered = records.OrderByDescending(x => x.CreateTimeMs).ToList();
        var items = ordered
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToItem(appId, x, 0))
            .ToList();
        return (items, ordered.Count);
    }

    /// <inheritdoc/>
    public async Task<(int TotalMemories, int UserCount)> GetAppStatsAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        var rows = await _databaseContext.AppMemoryShards.AsNoTracking()
            .Where(x => x.AppId == appId && x.MemoryCount > 0)
            .Select(x => x.MemoryCount)
            .ToListAsync(cancellationToken);
        return (rows.Sum(), rows.Count);
    }

    /// <inheritdoc/>
    public async Task DeleteAppCollectionAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        await _vectorStore.DeleteCollectionAsync(appId, cancellationToken);
        await _databaseContext.AppMemoryShards
            .Where(x => x.AppId == appId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsDeleted, 1L)
                .SetProperty(x => x.UpdateTime, DateTimeOffset.Now), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExtractAndConsolidateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _databaseContext.AppAgentSessions.AsNoTracking()
            .Where(x => x.Id == sessionId)
            .Select(x => new { x.Id, x.AppId, x.TeamId, x.CreateUserId, x.UserType })
            .FirstOrDefaultAsync(cancellationToken);
        if (session == null)
        {
            return;
        }

        // 外部会话不进入记忆（调试会话无落库行，天然跳过）
        if (session.UserType == (int)MoAI.Infra.Models.UserType.External
            || session.UserType == (int)MoAI.Infra.Models.UserType.ExternalApp)
        {
            return;
        }

        var resolution = await ResolveAsync(session.AppId, session.CreateUserId, cancellationToken);
        if (resolution is not { IsEnabled: true } || !resolution.Settings.AutoExtract)
        {
            return;
        }

        var settings = resolution.Settings;
        var conversation = await BuildConversationAsync(sessionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(conversation))
        {
            return;
        }

        var pair = await ResolveChatModelAsync(session.AppId, session.TeamId, cancellationToken);
        if (pair == null)
        {
            _logger.LogWarning("记忆提取跳过：应用 {AppId} 的对话模型不可用.", session.AppId);
            return;
        }

        var lockKey = $"moai:memory:extract:{sessionId:N}";
        var acquired = await _redisDatabase.Database.StringSetAsync(lockKey, "1", TimeSpan.FromMinutes(LockTtlMinutes), StackExchange.Redis.When.NotExists);
        if (!acquired)
        {
            return;
        }

        try
        {
            var facts = await ExtractFactsAsync(pair.Value.Model, pair.Value.Channel, conversation, settings, cancellationToken);
            if (facts.Count == 0)
            {
                return;
            }

            var applied = await ConsolidateAsync(session.AppId, session.TeamId, session.CreateUserId, sessionId, settings, pair.Value.Model, pair.Value.Channel, facts, cancellationToken);
            _logger.LogInformation("会话 {SessionId} 记忆提取完成：候选 {Candidates} 条，应用 {Applied} 条.", sessionId, facts.Count, applied);
        }
        catch (Exception ex)
        {
            // 记忆提取为尽力而为：失败不影响对话，后续轮次会再次触发
            _logger.LogWarning(ex, "会话 {SessionId} 记忆提取失败.", sessionId);
        }
        finally
        {
            await _redisDatabase.Database.KeyDeleteAsync(lockKey);
        }
    }

    /// <summary>
    /// mem0 式两段：事实抽取（选择性写入的过滤层）.
    /// </summary>
    private async Task<List<ExtractedFact>> ExtractFactsAsync(AiModelEntity model, AiChannelEntity channel, string conversation, AgentMemorySettings settings, CancellationToken cancellationToken)
    {
        var prompt = """
            你是对话记忆提取器。从对话片段中提取值得长期记住的信息（用户偏好与事实、重要事件与经历、有效的方法与教训）。
            只输出 JSON 数组，不要输出任何解释或代码块标记，格式：
            [{"content":"一句话记忆，必须包含具体细节（数字/名称/约定等）","type":"semantic|episodic|procedural","importance":3}]
            要求：
            - importance 取 1~5，只输出 importance>=3 的条目；寒暄、临时性问题、一次性任务细节不要提取
            - type 判定：稳定的事实/偏好/结论=semantic；发生的事件/经历=episodic；可复用的方法/流程/教训=procedural
            - 每条记忆必须独立可读，写明必要上下文（谁/什么/多少）
            - 最多 10 条；没有值得提取的信息时输出 []

            对话片段：
            """;
        var response = await _aiChatCompletionService.CompleteTextAsync(model, channel, prompt + conversation, new AiChatCompletionOptions
        {
            MaxOutputTokens = 1200,
            DisableThinking = true,
            Temperature = 0.1f,
        }, cancellationToken);

        var array = ExtractJsonArray(response);
        if (array is not { ValueKind: JsonValueKind.Array })
        {
            return [];
        }

        var facts = new List<ExtractedFact>();
        foreach (var node in array.Value.EnumerateArray())
        {
            var content = node.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.String
                ? contentNode.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var typeText = node.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String
                ? typeNode.GetString()
                : null;
            var importance = node.TryGetProperty("importance", out var importanceNode) && importanceNode.ValueKind == JsonValueKind.Number
                ? importanceNode.GetInt32()
                : 3;

            var type = typeText switch
            {
                "episodic" => AgentMemorySettings.TypeEpisodic,
                "procedural" => AgentMemorySettings.TypeProcedural,
                _ => AgentMemorySettings.TypeSemantic,
            };

            if (content.Length == 0 || importance < 3 || content.Length > MaxMemoryContentLength)
            {
                continue;
            }

            facts.Add(new ExtractedFact(content, type));
            if (facts.Count >= 10)
            {
                break;
            }
        }

        return facts;
    }

    /// <summary>
    /// mem0 式两段：与既有记忆合并决策（ADD/UPDATE/DELETE/NONE），返回应用条数.
    /// </summary>
    private async Task<int> ConsolidateAsync(
        Guid appId,
        int teamId,
        long userId,
        Guid sessionId,
        AgentMemorySettings settings,
        AiModelEntity model,
        AiChannelEntity channel,
        List<ExtractedFact> facts,
        CancellationToken cancellationToken)
    {
        // 既有记忆上下文：候选逐条向量召回 top2 + 最近记忆，去重后参与决策
        var existingIds = new HashSet<Guid>();
        var existingList = new List<(Guid Id, string Type, string Content)>();
        void AddExisting(AgentMemoryVectorRecord record)
        {
            if (existingIds.Add(record.Key))
            {
                existingList.Add((record.Key, TypeName(record.MemoryType), record.Content));
            }
        }

        foreach (var fact in facts)
        {
            var vector = await GenerateVectorAsync(appId, teamId, settings, fact.Content, cancellationToken);
            if (vector == null)
            {
                continue;
            }

            var hits = await _vectorStore.SearchAsync(appId, vector.Value, 2, userId, null, cancellationToken);
            foreach (var (record, _) in hits)
            {
                AddExisting(record);
            }
        }

        if (existingList.Count < 30)
        {
            var recent = await _vectorStore.ListUserAsync(appId, userId, ListMaxRecords, cancellationToken);
            foreach (var record in recent.OrderByDescending(x => x.CreateTimeMs).Take(30 - existingList.Count))
            {
                AddExisting(record);
            }
        }

        var existingJson = JsonSerializer.Serialize(existingList.Select(x => new { id = x.Id, type = x.Type, content = x.Content }), JsonOptions);
        var candidatesJson = JsonSerializer.Serialize(facts.Select(x => new { content = x.Content, type = TypeName(x.Type) }), JsonOptions);
        var prompt = $$"""
            你是记忆合并器。根据既有记忆对新提取的候选记忆逐条决定操作：
            - ADD：全新信息
            - UPDATE：与某条既有记忆是同一事实但有更新或补充，content 输出合并后的最新完整表述，id 填既有记忆 id
            - DELETE：候选明确否定某条既有记忆（如换机/取消/修正），id 填既有记忆 id，content 输出否定理由摘要
            - NONE：与既有记忆重复，或无长期价值
            只输出 JSON 数组，不要输出任何解释或代码块标记，格式：
            [{"action":"ADD","id":"","content":"...","type":"semantic"}]

            既有记忆：
            {{existingJson}}

            候选记忆：
            {{candidatesJson}}
            """;

        var response = await _aiChatCompletionService.CompleteTextAsync(model, channel, prompt, new AiChatCompletionOptions
        {
            MaxOutputTokens = 1500,
            DisableThinking = true,
            Temperature = 0.1f,
        }, cancellationToken);

        var array = ExtractJsonArray(response);
        if (array is not { ValueKind: JsonValueKind.Array })
        {
            // 决策解析失败时降级为全部直接写入（去重逻辑仍会合并高相似项）
            foreach (var fact in facts)
            {
                await SaveAsync(new MemorySaveRequest
                {
                    AppId = appId,
                    TeamId = teamId,
                    UserId = userId,
                    SessionId = sessionId,
                    Content = fact.Content,
                    MemoryType = fact.Type,
                    Settings = settings,
                }, cancellationToken);
            }

            return facts.Count;
        }

        var applied = 0;
        foreach (var node in array.Value.EnumerateArray())
        {
            var action = node.TryGetProperty("action", out var actionNode) ? actionNode.GetString()?.ToUpperInvariant() : null;
            var content = node.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.String
                ? contentNode.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var typeText = node.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String
                ? typeNode.GetString()
                : null;
            var type = typeText switch
            {
                "episodic" => AgentMemorySettings.TypeEpisodic,
                "procedural" => AgentMemorySettings.TypeProcedural,
                _ => AgentMemorySettings.TypeSemantic,
            };
            Guid relatedId = Guid.Empty;
            if (node.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String
                && Guid.TryParse(idNode.GetString(), out var parsedId))
            {
                relatedId = parsedId;
            }

            if (action == "NONE" || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (action == "DELETE" && relatedId != Guid.Empty)
            {
                if (await DeleteAsync(appId, relatedId, userId, cancellationToken))
                {
                    applied++;
                }

                continue;
            }

            if (action == "UPDATE" || action == "ADD")
            {
                if (action == "UPDATE" && relatedId != Guid.Empty)
                {
                    await DeleteAsync(appId, relatedId, userId, cancellationToken);
                }

                await SaveAsync(new MemorySaveRequest
                {
                    AppId = appId,
                    TeamId = teamId,
                    UserId = userId,
                    SessionId = sessionId,
                    Content = content,
                    MemoryType = type,
                    Settings = settings,
                }, cancellationToken);
                applied++;
            }
        }

        return applied;
    }

    /// <summary>
    /// 取会话最后一轮（最后一条用户消息起）的 user/assistant 文本对，作为提取素材.
    /// </summary>
    private async Task<string> BuildConversationAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var messages = await _databaseContext.AppAgentMessages.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.Seq)
            .Select(x => new { x.Role, x.Content })
            .ToListAsync(cancellationToken);

        var lastUserIndex = messages.FindLastIndex(x => string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase));
        if (lastUserIndex < 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var message in messages.Skip(lastUserIndex))
        {
            var isUser = string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase);
            var isAssistant = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase);
            if (!isUser && !isAssistant || string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            var content = message.Content.Length > MaxMessageLength ? message.Content[..MaxMessageLength] + "…" : message.Content;
            builder.AppendLine(isUser ? $"用户：{content}" : $"助手：{content}");
            if (builder.Length >= MaxConversationLength)
            {
                break;
            }
        }

        return builder.Length > MaxConversationLength ? builder.ToString()[..MaxConversationLength] : builder.ToString();
    }

    private async Task<AgentMemorySettings?> ResolveSettingsAsync(Guid appId, int teamId, int appType, int publishStatus, CancellationToken cancellationToken)
    {
        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == appId)
            .Select(x => new { x.ExecutionSettings, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            return null;
        }

        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(
            new AppEntity { Id = appId, TeamId = teamId, AppType = appType, PublishStatus = (short)publishStatus },
            new AppAgentConfigEntity { AppId = appId, TeamId = teamId, ExecutionSettings = config.ExecutionSettings, PublishedConfig = config.PublishedConfig },
            preferPublished: true);
        return AgentMemorySettings.ParseExecutionSettings(effective.ExecutionSettings);
    }

    /// <summary>
    /// 解析应用生效对话模型（ai_model join ai_channel，公共或已授权该团队），供提取 LLM 使用.
    /// </summary>
    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveChatModelAsync(Guid appId, int teamId, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == appId)
            .Select(x => new { x.Id, x.TeamId, x.AppType, x.PublishStatus })
            .FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            return null;
        }

        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == appId)
            .Select(x => new { x.ModelId, x.ExecutionSettings, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            return null;
        }

        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(
            new AppEntity { Id = appId, TeamId = teamId, AppType = app.AppType, PublishStatus = app.PublishStatus },
            new AppAgentConfigEntity { AppId = appId, TeamId = teamId, ModelId = config.ModelId, ExecutionSettings = config.ExecutionSettings, PublishedConfig = config.PublishedConfig },
            preferPublished: true);
        if (effective.ModelId == Guid.Empty)
        {
            return null;
        }

        return await ResolveModelPairAsync(effective.ModelId, teamId, cancellationToken);
    }

    /// <summary>
    /// 模型解析：启用中的模型与渠道，公共模型直用，否则要求已授权该团队.
    /// </summary>
    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveModelPairAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var rows = await _databaseContext.AiModels.AsNoTracking()
            .Where(x => x.Id == modelId && x.Enabled)
            .Join(
                _databaseContext.AiChannels.Where(c => c.Enabled),
                m => m.ChannelId,
                c => c.Id,
                (m, c) => new { Model = m, Channel = c, m.IsPublic })
            .FirstOrDefaultAsync(cancellationToken);
        if (rows == null)
        {
            return null;
        }

        if (!rows.IsPublic)
        {
            var authorized = await _databaseContext.AiModelAuthorizations.AsNoTracking()
                .AnyAsync(x => x.TeamId == teamId && x.AiModelId == modelId, cancellationToken);
            if (!authorized)
            {
                return null;
            }
        }

        return (rows.Model, rows.Channel);
    }

    private async Task<ReadOnlyMemory<float>?> GenerateVectorAsync(Guid appId, int teamId, AgentMemorySettings settings, string text, CancellationToken cancellationToken)
    {
        return await GenerateQueryVectorAsync(settings.EmbeddingModelId, settings.EmbeddingDimensions, teamId != 0 ? teamId : await ResolveTeamIdAsync(appId, cancellationToken), text, cancellationToken);
    }

    /// <summary>
    /// 生成查询向量：模型按团队授权解析失败时回落 teamId=0（公共模型口径），仍失败返回 null.
    /// </summary>
    private async Task<ReadOnlyMemory<float>?> GenerateQueryVectorAsync(Guid modelId, int dimensions, int teamId, string text, CancellationToken cancellationToken)
    {
        if (modelId == Guid.Empty || dimensions <= 0)
        {
            return null;
        }

        var pair = await ResolveModelPairAsync(modelId, teamId, cancellationToken)
            ?? (teamId != 0 ? await ResolveModelPairAsync(modelId, 0, cancellationToken) : null);
        if (pair == null)
        {
            return null;
        }

        var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(pair.Value.Model, pair.Value.Channel, cancellationToken);
        var embeddings = await generator.GenerateAsync([text], new EmbeddingGenerationOptions { Dimensions = dimensions }, cancellationToken);
        return embeddings.FirstOrDefault()?.Vector;
    }

    private async Task<int> ResolveTeamIdAsync(Guid appId, CancellationToken cancellationToken)
    {
        if (appId == Guid.Empty)
        {
            return 0;
        }

        return await _databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == appId)
            .Select(x => (int?)x.TeamId)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
    }

    private async Task<AppMemoryShardEntity?> GetShardAsync(Guid appId, long userId, CancellationToken cancellationToken)
    {
        return await _databaseContext.AppMemoryShards
            .FirstOrDefaultAsync(x => x.AppId == appId && x.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// 维护分片登记行数：无行且增量为正时建行，计数不允许为负.
    /// </summary>
    private async Task UpdateShardAsync(Guid appId, int teamId, long userId, int delta, CancellationToken cancellationToken)
    {
        if (delta == 0)
        {
            return;
        }

        var shard = await GetShardAsync(appId, userId, cancellationToken);
        if (shard == null)
        {
            if (delta <= 0)
            {
                return;
            }

            shard = new AppMemoryShardEntity
            {
                Id = Guid.CreateVersion7(),
                AppId = appId,
                TeamId = teamId,
                UserId = userId,
                MemoryCount = delta,
                LastActiveTime = DateTimeOffset.Now,
            };
            await _databaseContext.AppMemoryShards.AddAsync(shard, cancellationToken);
        }
        else
        {
            shard.MemoryCount = Math.Max(0, shard.MemoryCount + delta);
            if (delta > 0)
            {
                shard.LastActiveTime = DateTimeOffset.Now;
            }

            _databaseContext.AppMemoryShards.Update(shard);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static List<int> AllTypes()
        => new[] { AgentMemorySettings.TypeSemantic, AgentMemorySettings.TypeEpisodic, AgentMemorySettings.TypeProcedural }
            .ToList();

    private static string TypeName(int memoryType) => memoryType switch
    {
        AgentMemorySettings.TypeEpisodic => "episodic",
        AgentMemorySettings.TypeProcedural => "procedural",
        _ => "semantic",
    };

    private static MemoryItem ToItem(Guid appId, AgentMemoryVectorRecord record, double score) => new()
    {
        Id = record.Key,
        AppId = appId,
        MemoryType = record.MemoryType,
        Content = record.Content,
        Score = score,
        CreateTime = DateTimeOffset.FromUnixTimeMilliseconds(record.CreateTimeMs),
        UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(record.UpdateTimeMs),
    };

    private static JsonElement? ExtractJsonArray(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var start = response.IndexOf('[');
        var end = response.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(response[start..(end + 1)]);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private readonly record struct ExtractedFact(string Content, int Type);
}
