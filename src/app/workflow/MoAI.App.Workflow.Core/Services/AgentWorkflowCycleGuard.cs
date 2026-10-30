using System.Text.Json;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 流程 ↔ Agent 循环嵌套守卫（引擎侧入口）：保留对强类型 <see cref="WorkflowDefinition"/> 的 agentApp 节点收集，
/// 传递闭包检测统一由 <see cref="AppBindingCycleGuard"/> 承载（边含 Agent 的 Agent 应用工具、
/// Agent 的流程应用工具与流程 agentApp 节点三类，供 Agent 配置保存共用同一张图）.
/// </summary>
public static class AgentWorkflowCycleGuard
{
    /// <summary>
    /// 从流程定义收集全部 agentApp 节点引用的 Agent 应用 id.
    /// </summary>
    public static IReadOnlyCollection<Guid> CollectAgentAppIds(WorkflowDefinition definition)
    {
        var ids = new HashSet<Guid>();
        foreach (var node in definition.Nodes)
        {
            if (!string.Equals(node.Type, NodeTypes.AgentApp, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var config = node.GetConfig();
            if (config.ValueKind == JsonValueKind.Object
                && config.TryGetProperty("agentAppId", out var element)
                && element.ValueKind == JsonValueKind.String
                && Guid.TryParse(element.GetString(), out var id)
                && id != Guid.Empty)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// 校验从给定 Agent 应用出发的工具闭包不会引回当前流程应用，否则抛出 400.
    /// </summary>
    public static Task EnsureNoCycleAsync(DatabaseContext databaseContext, Guid workflowAppId, IReadOnlyCollection<Guid> agentAppIds, CancellationToken cancellationToken)
        => AppBindingCycleGuard.EnsureNoCycleFromWorkflowAsync(databaseContext, workflowAppId, agentAppIds, cancellationToken);

    /// <summary>
    /// 判断引入指定 Agent 应用是否与当前流程构成循环（供可选项查询标记）.
    /// </summary>
    public static Task<bool> IsCircularAsync(DatabaseContext databaseContext, Guid workflowAppId, Guid agentAppId, CancellationToken cancellationToken)
        => AppBindingCycleGuard.IsCircularFromWorkflowAsync(databaseContext, workflowAppId, agentAppId, cancellationToken);
}
