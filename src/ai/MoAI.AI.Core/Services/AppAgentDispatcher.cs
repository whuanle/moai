using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AppAgentDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppAgentDispatcher"/> class.
    /// </summary>
    /// <param name="sessionShell">仅用于会话序列化兼容的占位 Agent.</param>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="logger">日志（对话装配/运行异常记录，避免静默失败）.</param>
    public AppAgentDispatcher(AIAgent sessionShell, IServiceScopeFactory scopeFactory, ILogger<AppAgentDispatcher> logger)
        : base(sessionShell)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 作用域同步释放会调用收集器 IDisposable 兜底：一轮对话聚合的使用量在结束时批量推送统计
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
        // 作用域同步释放会调用收集器 IDisposable 兜底：SSE 流结束后批量推送本轮统计
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
            _logger.LogError(ex, "Agent 装配失败（resolve 阶段）.");
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
            // 子代理过程旁路通道（app sdd D53）：子代理工具执行期间内层流挂起，合并循环竞争读出进度事件，
            // 以合成更新注入同一 SSE 流（实时嵌套活动卡的来源）；无子代理派发时通道恒空，行为与直通一致
            var progressChannel = SubAgentProgressChannel.CreateChannel();
            Task<bool>? moveNextTask = null;
            while (true)
            {
                AgentResponseUpdate? update = null;
                string? runError = null;
                var runFinished = false;
                try
                {
                    if (moveNextTask == null)
                    {
                        // 每次内层 MoveNextAsync 前登记发布通道（异步迭代器的 EC 修改不跨段保留）：
                        // 该次等待子树（MAF 函数调用循环 → 工具执行 → SubAgentInvoker.Publish）由此看到通道
                        SubAgentProgressChannel.SetPublisher(progressChannel);
                        moveNextTask = enumerator.MoveNextAsync().AsTask();
                    }

                    var readTask = progressChannel.Reader.WaitToReadAsync(cancellationToken).AsTask();
                    var completed = await Task.WhenAny(moveNextTask, readTask).ConfigureAwait(false);
                    if (completed == moveNextTask)
                    {
                        if (!await moveNextTask.ConfigureAwait(false))
                        {
                            runFinished = true;
                        }
                        else
                        {
                            update = enumerator.Current;
                            moveNextTask = null;
                        }
                    }
                    else if (cancellationToken.IsCancellationRequested)
                    {
                        // 通道等待被取消（请求断开）：走统一失败文案收尾，避免空转
                        runError = Describe(new OperationCanceledException(cancellationToken));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Agent 运行失败（run 阶段）.");
                    runError = Describe(ex);
                }

                // 冲刷窗口期写入的进度事件（先于本轮内层块产出，保持时间顺序）；yield 不可位于带 catch 的 try 内，故置于循环体
                while (progressChannel.Reader.TryRead(out var evt))
                {
                    yield return SubAgentProgressChannel.ToUpdate(evt);
                }

                if (runError != null)
                {
                    yield return new AgentResponseUpdate(ChatRole.Assistant, runError);
                    yield break;
                }

                if (runFinished)
                {
                    break;
                }

                if (update != null)
                {
                    yield return update;
                }
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
        // 安全审计事件归属：从 StateBag 读取（AppSecurityAgentMiddleware）；StateBag 值约束为引用类型，long 以字符串存放
        session.StateBag.SetValue(AppAgentConstants.UserIdStateKey, userId.ToString(System.Globalization.CultureInfo.InvariantCulture));

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

            return await factory.CreateAsync(row.AppId, row.TeamId, userId, sessionId, false, row.PromptId, cancellationToken, ResolveApprovalMode(serviceProvider), ResolveWorkflowDraft(serviceProvider), ResolveUiToolsEnabled(serviceProvider), ResolveRequestedModelId(serviceProvider), ResolveRequestedTeamAgentId(serviceProvider)).ConfigureAwait(false);
        }

        // 无正式会话行：回落调试会话注册表（Redis）；命中且本人时按调试装配，不落库、不计数
        var registry = serviceProvider.GetRequiredService<IDebugSessionRegistry>();
        var debug = await registry.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (debug == null || debug.UserId != userId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        return await factory.CreateAsync(debug.AppId, debug.TeamId, userId, sessionId, true, 0, cancellationToken, ResolveApprovalMode(serviceProvider), ResolveWorkflowDraft(serviceProvider), ResolveUiToolsEnabled(serviceProvider), ResolveRequestedModelId(serviceProvider), ResolveRequestedTeamAgentId(serviceProvider)).ConfigureAwait(false);
    }

    /// <summary>
    /// 从对话 SSE 请求头解析工具审批模式（X-Moai-Tool-Approval），
    /// 非对话渠道（飞书等）无 HttpContext 时按自动模式处理.
    /// </summary>
    /// <param name="serviceProvider">请求作用域服务.</param>
    /// <returns>审批模式.</returns>
    private static string ResolveApprovalMode(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
        var mode = httpContext?.Request.Headers[AppToolApprovalContract.HeaderName].ToString();
        return AppToolApprovalContract.IsValidMode(mode) ? mode! : AppToolApprovalContract.ModeAuto;
    }

    /// <summary>
    /// 从对话 SSE 请求头解析流程草稿执行标记（X-Moai-Workflow-Draft=1，工作台「调试」Tab 携带），
    /// 非对话渠道或未携带时按已发布快照执行.
    /// </summary>
    /// <param name="serviceProvider">请求作用域服务.</param>
    /// <returns>是否按最新草稿执行.</returns>
    private static bool ResolveWorkflowDraft(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
        return httpContext?.Request.Headers[AppAgentConstants.WorkflowDraftHeaderName].ToString() == "1";
    }

    /// <summary>
    /// 从对话 SSE 请求头解析前端展示工具开关（X-Moai-Ui-Tools=1，前端已适配侧边栏渲染时携带），
    /// 未携带按关闭处理（嵌入组件/外部渠道不注册 ui_ 前缀工具）.
    /// </summary>
    /// <param name="serviceProvider">请求作用域服务.</param>
    /// <returns>是否注册前端展示工具.</returns>
    private static bool ResolveUiToolsEnabled(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
        return httpContext?.Request.Headers[FrontendToolContract.HeaderName].ToString() == "1";
    }

    /// <summary>
    /// 从对话 SSE 请求头解析用户所选对话模型（X-Moai-Model-Id，值为 ai_model.id 的 uuid），
    /// 非法值按未携带处理（装配时回退默认模型）.
    /// </summary>
    /// <param name="serviceProvider">请求作用域服务.</param>
    /// <returns>所选模型 id，未携带或非法时为 null.</returns>
    private static Guid? ResolveRequestedModelId(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
        var raw = httpContext?.Request.Headers[AppAgentConstants.ModelHeaderName].ToString();
        return Guid.TryParse(raw, out var modelId) && modelId != Guid.Empty ? modelId : null;
    }

    /// <summary>
    /// 从对话 SSE 请求头解析用户所选团队智能体（X-Moai-Team-Agent-Id，值为 team_agent.id 的 uuid），
    /// 非法值按未携带处理（装配时回退默认智能体）.
    /// </summary>
    /// <param name="serviceProvider">请求作用域服务.</param>
    /// <returns>所选团队智能体 id，未携带或非法时为 null.</returns>
    private static Guid? ResolveRequestedTeamAgentId(IServiceProvider serviceProvider)
    {
        var httpContext = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
        var raw = httpContext?.Request.Headers[AppAgentConstants.TeamAgentHeaderName].ToString();
        return Guid.TryParse(raw, out var teamAgentId) && teamAgentId != Guid.Empty ? teamAgentId : null;
    }
}
