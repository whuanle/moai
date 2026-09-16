using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AI;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// AG-UI 动态派发 Agent：单个注册实例，按请求（路由 appId + 用户 + 会话）装配真正的应用 Agent.
/// </summary>
public sealed class AppAgentDispatcher : DelegatingAIAgent
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppAgentDispatcher"/> class.
    /// </summary>
    /// <param name="sessionShell">仅用于会话序列化兼容的占位 Agent.</param>
    /// <param name="scopeFactory">作用域工厂.</param>
    public AppAgentDispatcher(AIAgent sessionShell, IServiceScopeFactory scopeFactory)
        : base(sessionShell)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc/>
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        try
        {
            var inner = await ResolveInnerAsync(scope, session, cancellationToken).ConfigureAwait(false);
            return await inner.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var text = ex is BusinessException be && be.StatusCode == 404
                ? AppAgentConstants.SessionResolveErrorMarker + Describe(ex)
                : Describe(ex);
            return new AgentResponse(new ChatMessage(ChatRole.Assistant, text));
        }
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        AIAgent? inner = null;
        string? resolveError = null;
        var resolveSessionMissing = false;
        try
        {
            inner = await ResolveInnerAsync(scope, session, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            resolveError = Describe(ex);
            resolveSessionMissing = ex is BusinessException be && be.StatusCode == 404;
        }

        if (resolveError != null)
        {
            var text = resolveSessionMissing ? AppAgentConstants.SessionResolveErrorMarker + resolveError : resolveError;
            yield return new AgentResponseUpdate(ChatRole.Assistant, text);
            yield break;
        }

        // 运行期异常也要转成可见文本，避免 SSE 已开始响应后中断导致前端得到空回复.
        var enumerator = inner!.RunStreamingAsync(messages, session, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using (enumerator.ConfigureAwait(false))
        {
            while (true)
            {
                AgentResponseUpdate? update = null;
                string? runError = null;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (Exception ex)
                {
                    runError = Describe(ex);
                }

                if (runError != null)
                {
                    yield return new AgentResponseUpdate(ChatRole.Assistant, runError);
                    yield break;
                }

                yield return update!;
            }
        }
    }

    private static string Describe(Exception ex)
        => ex is BusinessException be ? be.Message : "对话运行失败，请稍后重试。";

    private static async Task<AIAgent> ResolveInnerAsync(IServiceScope scope, AgentSession? session, CancellationToken cancellationToken)
    {
        if (session == null)
        {
            throw new BusinessException("会话上下文缺失.") { StatusCode = 400 };
        }

        var sessionIdValue = session.StateBag.GetValue<string>(AppAgentConstants.SessionIdStateKey);
        if (!Guid.TryParse(sessionIdValue, out var sessionId))
        {
            throw new BusinessException("会话标识无效.") { StatusCode = 400 };
        }

        var serviceProvider = scope.ServiceProvider;
        var databaseContext = serviceProvider.GetRequiredService<DatabaseContext>();
        var userContextProvider = serviceProvider.GetRequiredService<IUserContextProvider>();
        var userId = userContextProvider.GetUserContext().UserId;

        var row = await databaseContext.AppAgentSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new { x.AppId, x.TeamId, x.CreateUserId, x.PromptId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var factory = serviceProvider.GetRequiredService<AppAgentFactory>();

        if (row != null)
        {
            // 正式会话：会话不属于当前用户时按不存在处理，避免越权续聊
            if (row.CreateUserId != userId)
            {
                throw new BusinessException("会话不存在.") { StatusCode = 404 };
            }

            return await factory.CreateAsync(row.AppId, row.TeamId, userId, sessionId, false, row.PromptId, cancellationToken).ConfigureAwait(false);
        }

        // 无正式会话行：回落调试会话注册表（Redis）；命中且本人时按调试装配，不落库、不计数
        var registry = serviceProvider.GetRequiredService<IDebugSessionRegistry>();
        var debug = await registry.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (debug == null || debug.UserId != userId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        return await factory.CreateAsync(debug.AppId, debug.TeamId, userId, sessionId, true, 0, cancellationToken).ConfigureAwait(false);
    }
}
