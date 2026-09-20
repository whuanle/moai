using System.Text.Json.Nodes;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using MoAI.AI.Services;
using MoAI.App.Workflow.Nodes;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// Agent 应用调用端口实现：agentApp 节点的引擎端口适配器——
/// 负责工作流域校验与防线（应用存在/未禁用/Agent 类型/同团队/已发布 + 以根流程为基准的环检测），
/// 解析发布快照生效配置后委托 AI 模块统一入口（<see cref="IWorkflowNodeAiInvoker"/>）
/// 装配模型与工具链执行，本类不接入模型.
/// </summary>
[InjectOnScoped]
public class WorkflowAgentAppClient : IWorkflowAgentAppClient
{
    private readonly DatabaseContext _databaseContext;
    private readonly IWorkflowNodeAiInvoker _aiInvoker;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAgentAppClient"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="aiInvoker">AI 模块统一模型接入入口.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    public WorkflowAgentAppClient(
        DatabaseContext databaseContext,
        IWorkflowNodeAiInvoker aiInvoker,
        WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _aiInvoker = aiInvoker;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<string> InvokeAsync(Guid agentAppId, string prompt, JsonArray? history, CancellationToken cancellationToken)
    {
        // 运行期环检测防线：以根流程为基准（嵌套执行时 Scoped 上下文的 AppId 已被内层覆盖）
        var rootAppId = WorkflowRootContext.RootAppId ?? _executionContext.AppId;
        await AgentWorkflowCycleGuard.EnsureNoCycleAsync(_databaseContext, rootAppId, [agentAppId], cancellationToken);

        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == agentAppId, cancellationToken)
            ?? throw new WorkflowException("Agent 应用不存在");

        if (app.IsDisable)
        {
            throw new WorkflowException("Agent 应用已被禁用");
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new WorkflowException("所选应用不是 Agent 应用");
        }

        if (app.TeamId != (int)_executionContext.TeamId)
        {
            throw new WorkflowException("Agent 应用不属于本团队");
        }

        if (app.PublishStatus != 1)
        {
            throw new WorkflowException("Agent 应用尚未发布，无法在流程中调用");
        }

        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == agentAppId, cancellationToken)
            ?? throw new WorkflowException("Agent 应用未配置对话模型");

        // 正式调用按发布快照执行（与 Agent 对话/流程工具绑定一致）
        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: true);
        if (effective.ModelId == Guid.Empty)
        {
            throw new WorkflowException("Agent 应用未配置对话模型");
        }

        return await _aiInvoker.AgentAppAsync(new WorkflowNodeAgentAppRequest
        {
            App = app,
            Config = effective,
            UserId = _executionContext.UserId,
            Prompt = prompt,
            History = ConvertHistory(history),
        }, cancellationToken);
    }

    /// <summary>引擎历史契约 [{role, content}] 转模型消息（空内容跳过，非 assistant 一律按 user）.</summary>
    private static List<ChatMessage>? ConvertHistory(JsonArray? history)
    {
        if (history == null)
        {
            return null;
        }

        var messages = new List<ChatMessage>();
        foreach (var item in history.OfType<JsonObject>())
        {
            var role = item["role"]?.GetValue<string>();
            var content = item["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            messages.Add(new ChatMessage(
                string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User,
                content));
        }

        return messages;
    }
}
