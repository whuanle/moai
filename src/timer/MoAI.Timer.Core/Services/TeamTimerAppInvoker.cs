using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using MoAI.AI;
using MoAI.AI.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Timer.Services;

/// <summary>
/// 定时器应用无头调用器：按绑定应用的实际类型分发——
/// Agent 应用走正式会话链路（<see cref="AppAgentFactory.CreateAsync"/> 发布快照 + token 记账 + PG 消息落库，
/// 与网页对话/飞书渠道同口径），会话标记 <see cref="UserType.Timer"/>，仅管理员在应用日志页可见；
/// 流程应用走已发布定义（<see cref="IWorkflowAppChatInvoker"/>），记录流程实例 id.
/// </summary>
[InjectOnScoped]
public class TeamTimerAppInvoker
{
    private readonly DatabaseContext _databaseContext;
    private readonly AppAgentFactory _agentFactory;
    private readonly AppChatHotStore _hotStore;
    private readonly AppChatFlushService _flushService;
    private readonly IWorkflowAppChatInvoker _workflowInvoker;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerAppInvoker"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="agentFactory">Agent 应用正式装配工厂.</param>
    /// <param name="hotStore">会话热态存储.</param>
    /// <param name="flushService">会话落库服务.</param>
    /// <param name="workflowInvoker">流程应用无头调用端口.</param>
    public TeamTimerAppInvoker(DatabaseContext databaseContext, AppAgentFactory agentFactory, AppChatHotStore hotStore, AppChatFlushService flushService, IWorkflowAppChatInvoker workflowInvoker)
    {
        _databaseContext = databaseContext;
        _agentFactory = agentFactory;
        _hotStore = hotStore;
        _flushService = flushService;
        _workflowInvoker = workflowInvoker;
    }

    /// <summary>
    /// 无头执行一次绑定的应用.
    /// </summary>
    /// <param name="timer">定时器.</param>
    /// <param name="app">绑定的应用（调用方已校验存在且未禁用）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>回答文本、流程实例 id（Agent 应用为 null）与产生的会话 id（流程应用为 null）；失败抛异常.</returns>
    public async Task<(string Result, string? AppInstanceId, Guid? SessionId)> InvokeAsync(TeamTimerEntity timer, AppEntity app, CancellationToken cancellationToken)
    {
        if (app.AppType == (int)AppType.Agent)
        {
            return await InvokeAgentAppAsync(timer, app, cancellationToken);
        }

        if (app.AppType == (int)AppType.Workflow)
        {
            return await InvokeWorkflowAppAsync(timer, app, cancellationToken);
        }

        throw new BusinessException("绑定的应用类型不支持定时执行.") { StatusCode = 400 };
    }

    private async Task<(string Result, string? AppInstanceId, Guid? SessionId)> InvokeAgentAppAsync(TeamTimerEntity timer, AppEntity app, CancellationToken cancellationToken)
    {
        if (app.PublishStatus != 1)
        {
            throw new BusinessException("Agent 应用尚未发布，无法执行.") { StatusCode = 400 };
        }

        // 每次执行一个独立会话，UserType=Timer 标记来源：管理员在应用日志页可见，本人会话列表排除
        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = timer.TeamId,
            AppId = app.Id,
            Title = AppAgentConstants.DefaultSessionTitle,
            UserType = (int)UserType.Timer,
            CreateUserId = timer.CreateUserId,
            LastMessageTime = DateTimeOffset.Now,
        };
        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 正式装配（发布快照 + UsageCapturing token 记账 + Postgres 落库历史），与飞书渠道同口径
        var agent = await _agentFactory.CreateAsync(app.Id, app.TeamId, timer.CreateUserId, session.Id, isDebug: false, promptId: 0, cancellationToken);

        var agentSession = await LoadSessionAsync(agent, session.Id, cancellationToken);
        var response = await agent.RunAsync([new ChatMessage(ChatRole.User, timer.InputContent)], agentSession, cancellationToken: cancellationToken);
        var reply = response.Messages?.LastOrDefault(x => x.Role == ChatRole.Assistant)?.Text ?? string.Empty;

        // 快照写热态并落库（压缩消息 + token 聚合回写 + AI 提炼标题），日志页与消息详情即可见
        var serialized = await agent.SerializeSessionAsync(agentSession, cancellationToken: cancellationToken);
        await _hotStore.SetSessionSnapshotAsync(session.Id, serialized.GetRawText(), cancellationToken);
        await _flushService.FlushAsync(session.Id, cancellationToken);

        return (reply, null, session.Id);
    }

    /// <summary>
    /// 加载会话热态快照（新会话走 <see cref="AIAgent.CreateSessionAsync"/>），并回填 SessionId 状态键.
    /// </summary>
    private async Task<AgentSession> LoadSessionAsync(AIAgent agent, Guid sessionId, CancellationToken cancellationToken)
    {
        AgentSession session;
        var snapshot = await _hotStore.GetSessionSnapshotAsync(sessionId, cancellationToken);
        if (!string.IsNullOrEmpty(snapshot))
        {
            using var document = System.Text.Json.JsonDocument.Parse(snapshot);
            session = await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
        }
        else
        {
            session = await agent.CreateSessionAsync(cancellationToken);
        }

        session.StateBag.SetValue(AppAgentConstants.SessionIdStateKey, sessionId.ToString());
        return session;
    }

    private async Task<(string Result, string? AppInstanceId, Guid? SessionId)> InvokeWorkflowAppAsync(TeamTimerEntity timer, AppEntity app, CancellationToken cancellationToken)
    {
        var result = await _workflowInvoker.InvokeAsync(new WorkflowAppChatRequest
        {
            AppId = app.Id,
            TeamId = timer.TeamId,
            UserId = timer.CreateUserId,
            SessionId = Guid.CreateVersion7(),
            Query = timer.InputContent,
            UseDraft = false,
        }, cancellationToken);

        if (!result.Success)
        {
            throw new BusinessException(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "流程应用执行未完成." : result.ErrorMessage) { StatusCode = 400 };
        }

        return (result.Reply, result.InstanceId, null);
    }
}
