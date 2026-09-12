using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.AI.Services;

/// <summary>
/// Agent 会话存储：会话态写 Redis 热态，完成后触发一次性落库（压缩后视图 + PG 冷快照）.
/// </summary>
public sealed class AppAgentSessionStore : AgentSessionStore
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppAgentSessionStore"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    public AppAgentSessionStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc/>
    public override async ValueTask SaveSessionAsync(AIAgent agent, string sessionStoreId, AgentSession session, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(sessionStoreId, out var sessionId))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var hotStore = scope.ServiceProvider.GetRequiredService<AppChatHotStore>();
        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
        await hotStore.SetSessionSnapshotAsync(sessionId, serialized.GetRawText(), cancellationToken).ConfigureAwait(false);

        var flushService = scope.ServiceProvider.GetRequiredService<AppChatFlushService>();
        await flushService.FlushAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async ValueTask<AgentSession> GetSessionAsync(AIAgent agent, string sessionStoreId, CancellationToken cancellationToken = default)
    {
        AgentSession session;

        if (Guid.TryParse(sessionStoreId, out var sessionId))
        {
            using var scope = _scopeFactory.CreateScope();
            var hotStore = scope.ServiceProvider.GetRequiredService<AppChatHotStore>();
            var snapshot = await hotStore.GetSessionSnapshotAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(snapshot))
            {
                using var document = JsonDocument.Parse(snapshot);
                session = await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        }

        session.StateBag.SetValue(AppAgentConstants.SessionIdStateKey, sessionStoreId);
        return session;
    }

    /// <inheritdoc/>
    public override async ValueTask DeleteSessionAsync(AIAgent agent, string sessionStoreId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(sessionStoreId, out var sessionId))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var hotStore = scope.ServiceProvider.GetRequiredService<AppChatHotStore>();
        await hotStore.ClearAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }
}
