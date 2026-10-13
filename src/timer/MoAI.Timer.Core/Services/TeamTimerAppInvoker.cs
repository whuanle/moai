using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Services;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.Timer.Services;

/// <summary>
/// 定时器应用无头调用器：按绑定应用的实际类型分发——
/// Agent 应用走发布快照生效配置（<see cref="IWorkflowNodeAiInvoker.AgentAppAsync"/>，
/// 与流程 agentApp 节点/正式对话同口径）；流程应用走已发布定义（<see cref="IWorkflowAppChatInvoker"/>），
/// 两者均返回完整回答文本，不产生用户可见会话.
/// </summary>
[InjectOnScoped]
public class TeamTimerAppInvoker
{
    private readonly DatabaseContext _databaseContext;
    private readonly IWorkflowNodeAiInvoker _agentInvoker;
    private readonly IWorkflowAppChatInvoker _workflowInvoker;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerAppInvoker"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="agentInvoker">Agent 应用无头调用端口.</param>
    /// <param name="workflowInvoker">流程应用无头调用端口.</param>
    public TeamTimerAppInvoker(DatabaseContext databaseContext, IWorkflowNodeAiInvoker agentInvoker, IWorkflowAppChatInvoker workflowInvoker)
    {
        _databaseContext = databaseContext;
        _agentInvoker = agentInvoker;
        _workflowInvoker = workflowInvoker;
    }

    /// <summary>
    /// 无头执行一次绑定的应用.
    /// </summary>
    /// <param name="timer">定时器.</param>
    /// <param name="app">绑定的应用（调用方已校验存在且未禁用）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>回答文本与流程实例 id（Agent 应用为 null）；失败抛异常.</returns>
    public async Task<(string Result, string? AppInstanceId)> InvokeAsync(TeamTimerEntity timer, AppEntity app, CancellationToken cancellationToken)
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

    private async Task<(string Result, string? AppInstanceId)> InvokeAgentAppAsync(TeamTimerEntity timer, AppEntity app, CancellationToken cancellationToken)
    {
        if (app.PublishStatus != 1)
        {
            throw new BusinessException("Agent 应用尚未发布，无法执行.") { StatusCode = 400 };
        }

        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken)
            ?? throw new BusinessException("Agent 应用未配置对话模型.") { StatusCode = 400 };

        // 正式执行按发布快照（与 Agent 对话/流程 agentApp 节点一致）
        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: true);
        if (effective.ModelId == Guid.Empty)
        {
            throw new BusinessException("Agent 应用未配置对话模型.") { StatusCode = 400 };
        }

        var reply = await _agentInvoker.AgentAppAsync(new WorkflowNodeAgentAppRequest
        {
            App = app,
            Config = effective,
            UserId = timer.CreateUserId,
            Prompt = timer.InputContent,
        }, cancellationToken);

        return (reply, null);
    }

    private async Task<(string Result, string? AppInstanceId)> InvokeWorkflowAppAsync(TeamTimerEntity timer, AppEntity app, CancellationToken cancellationToken)
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

        return (result.Reply, result.InstanceId);
    }
}
