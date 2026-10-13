using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Maomi;
using MoAI.Database.Entities;
namespace MoAI.AI.Services;

/// <summary>
/// 应用安全事件写入器：把一次运行聚合的规则命中写入有界通道，由后台服务异步落库.
/// <para>通道满时丢弃最旧事件（审计尽力而为，不反压对话主链路）；进程崩溃丢缓冲内事件.</para>
/// </summary>
[InjectOnScoped]
public sealed class AppSecurityAuditWriter
{
    private readonly AppSecurityAuditChannel _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSecurityAuditWriter"/> class.
    /// </summary>
    /// <param name="channel">单例事件通道.</param>
    public AppSecurityAuditWriter(AppSecurityAuditChannel channel)
    {
        _channel = channel;
    }

    /// <summary>
    /// 写入一次运行聚合的命中（每条聚合行一个事件实体）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="sessionId">会话 id，可空.</param>
    /// <param name="userId">触发用户 id，可空.</param>
    /// <param name="hits">聚合命中列表.</param>
    public void Write(Guid appId, int teamId, Guid? sessionId, long? userId, IReadOnlyList<(string Scope, string RuleName, string RuleType, string Strategy, string Action, int Count)> hits)
    {
        if (hits.Count == 0)
        {
            return;
        }

        foreach (var hit in hits)
        {
            _channel.Queue.Writer.TryWrite(new AppSecurityEventEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = teamId,
                AppId = appId,
                SessionId = sessionId,
                UserId = userId,
                Scope = hit.Scope,
                RuleName = hit.RuleName ?? string.Empty,
                RuleType = hit.RuleType ?? string.Empty,
                Strategy = hit.Strategy,
                Action = hit.Action,
                HitCount = hit.Count,
            });
        }
    }
}

/// <summary>
/// 安全事件单例通道（有界，满时丢弃最旧）.
/// </summary>
public sealed class AppSecurityAuditChannel
{
    /// <summary>
    /// 事件容量上限（超出丢弃最旧）.
    /// </summary>
    public const int Capacity = 10_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSecurityAuditChannel"/> class.
    /// </summary>
    public AppSecurityAuditChannel()
    {
        Queue = Channel.CreateBounded<AppSecurityEventEntity>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });
    }

    /// <summary>
    /// 事件队列.
    /// </summary>
    public Channel<AppSecurityEventEntity> Queue { get; }
}
