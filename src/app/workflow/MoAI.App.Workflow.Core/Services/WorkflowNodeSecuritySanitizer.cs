using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Extensions.Logging;
using MoAI.AI.Services;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.Database.Aggregates;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// <inheritdoc cref="INodeDataSanitizer"/>
/// 按当前执行上下文的应用安全策略（app_security_config）对流程节点数据脱敏：
/// 节点输入按「工具参数」范围、节点输出按「工具结果」范围（AI 对话/Agent 应用节点输出额外叠加「模型回复」范围）、
/// 错误消息按「工具结果」范围；策略在同一作用域内记忆化（一轮流程只查一次库）.
/// </summary>
[InjectOnScoped]
public sealed class WorkflowNodeSecuritySanitizer : INodeDataSanitizer
{
    private readonly AppSecurityService _appSecurityService;
    private readonly WorkflowExecutionContext _executionContext;
    private readonly ILogger<WorkflowNodeSecuritySanitizer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowNodeSecuritySanitizer"/> class.
    /// </summary>
    /// <param name="appSecurityService">应用内容脱敏策略读取服务.</param>
    /// <param name="executionContext">工作流执行上下文（取当前应用 id）.</param>
    /// <param name="logger">日志.</param>
    public WorkflowNodeSecuritySanitizer(AppSecurityService appSecurityService, WorkflowExecutionContext executionContext, ILogger<WorkflowNodeSecuritySanitizer> logger)
    {
        _appSecurityService = appSecurityService;
        _executionContext = executionContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SanitizeAsync(WorkflowInstance instance, NodeDefinition node, NodeExecutionState state, CancellationToken cancellationToken)
    {
        if (_executionContext.AppId == Guid.Empty)
        {
            return;
        }

        var policy = await _appSecurityService.GetPolicyAsync(_executionContext.AppId, cancellationToken).ConfigureAwait(false);
        if (!policy.IsActive)
        {
            return;
        }

        try
        {
            // 节点输入按「工具参数」范围、节点输出按「工具结果」范围（内容规则）；
            // AI 对话/Agent 应用节点的输出即模型回复，额外按模型回复专属规则叠加脱敏
            policy.MaskToolArgsJson(state.Input);
            policy.MaskToolResultJson(state.Output);
            if (node.Type is NodeTypes.AiChat or NodeTypes.AgentApp)
            {
                policy.MaskModelJson(state.Output);
            }

            if (!string.IsNullOrEmpty(state.ErrorMessage))
            {
                state.ErrorMessage = policy.MaskToolResultText(state.ErrorMessage);
            }
        }
        catch (System.Exception ex)
        {
            // 脱敏失败不阻断流程执行，仅记录日志
            _logger.LogWarning(ex, "流程节点数据脱敏失败. AppId={AppId} NodeKey={NodeKey}", _executionContext.AppId, node.Key);
        }
    }
}
