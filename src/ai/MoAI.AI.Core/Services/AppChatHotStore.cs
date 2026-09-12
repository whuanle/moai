using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.AI.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// Agent 会话热态存储（Redis）：运行期间消息只写热态，流结束后由 flush 一次性落库，提升多轮性能.
/// </summary>
[InjectOnSingleton]
public class AppChatHotStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppChatHotStore"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public AppChatHotStore(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 读取会话热态消息.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>消息记录（按 seq 升序）.</returns>
    public async Task<List<AppAgentMessageRecord>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var list = await _redisDatabase.GetAsync<List<AppAgentMessageRecord>>(MessagesKey(sessionId));
        return list ?? [];
    }

    /// <summary>
    /// 追加消息到会话热态.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="records">新增消息.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task AppendMessagesAsync(Guid sessionId, IReadOnlyList<AppAgentMessageRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var key = MessagesKey(sessionId);
        var list = await _redisDatabase.GetAsync<List<AppAgentMessageRecord>>(key) ?? [];
        list.AddRange(records);
        await _redisDatabase.Database.StringSetAsync(key, list.ToRedisValue(), Ttl);
    }

    /// <summary>
    /// 读取会话快照（AgentSession 序列化 JSON）.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>快照 JSON；不存在返回 null.</returns>
    public async Task<string?> GetSessionSnapshotAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _redisDatabase.GetAsync<AppAgentSessionSnapshot>(SessionKey(sessionId));
        return string.IsNullOrEmpty(snapshot?.Json) ? null : snapshot!.Json;
    }

    /// <summary>
    /// 写入会话快照.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="json">AgentSession 序列化 JSON.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task SetSessionSnapshotAsync(Guid sessionId, string json, CancellationToken cancellationToken = default)
    {
        await _redisDatabase.Database.StringSetAsync(SessionKey(sessionId), new AppAgentSessionSnapshot { Json = json }.ToRedisValue(), Ttl);
    }

    /// <summary>
    /// 累加会话 token 用量.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="promptTokens">输入 token.</param>
    /// <param name="completionTokens">输出 token.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task AddUsageAsync(Guid sessionId, int promptTokens, int completionTokens, CancellationToken cancellationToken = default)
    {
        if (promptTokens <= 0 && completionTokens <= 0)
        {
            return;
        }

        var key = UsageKey(sessionId);
        var usage = await _redisDatabase.GetAsync<AppAgentUsageAggregate>(key) ?? new AppAgentUsageAggregate();
        usage.InputTokens += Math.Max(0, promptTokens);
        usage.OutTokens += Math.Max(0, completionTokens);
        usage.TotalTokens = usage.InputTokens + usage.OutTokens;
        await _redisDatabase.Database.StringSetAsync(key, usage.ToRedisValue(), Ttl);
    }

    /// <summary>
    /// 读取会话 token 用量.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用量聚合.</returns>
    public async Task<AppAgentUsageAggregate> GetUsageAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => await _redisDatabase.GetAsync<AppAgentUsageAggregate>(UsageKey(sessionId)) ?? new AppAgentUsageAggregate();

    /// <summary>
    /// 清除会话热态（快照过期后由 PG 冷快照恢复）.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task ClearAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _redisDatabase.Database.KeyDeleteAsync(MessagesKey(sessionId));
        await _redisDatabase.Database.KeyDeleteAsync(SessionKey(sessionId));
        await _redisDatabase.Database.KeyDeleteAsync(UsageKey(sessionId));
    }

    private static string MessagesKey(Guid sessionId) => $"appagent:msg:{sessionId:N}";

    private static string SessionKey(Guid sessionId) => $"appagent:session:{sessionId:N}";

    private static string UsageKey(Guid sessionId) => $"appagent:usage:{sessionId:N}";
}
