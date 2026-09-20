using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Definition;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 流程 ↔ Agent 循环嵌套守卫：流程可经 agentApp 节点调用 Agent 应用，Agent 应用又可把流程绑定为工具
/// （app_agent_config.workflow_apps），二者构成有向图。本守卫沿「Agent → 流程工具 → 其 agentApp 节点 → Agent」
/// 做传递闭包检测，任一路径引回当前流程即视为循环，在保存草稿、发布、调试执行与运行期节点调用时拦截.
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
    public static async Task EnsureNoCycleAsync(DatabaseContext databaseContext, Guid workflowAppId, IReadOnlyCollection<Guid> agentAppIds, CancellationToken cancellationToken)
    {
        if (workflowAppId == Guid.Empty || agentAppIds.Count == 0)
        {
            return;
        }

        var (circular, chain) = await FindCycleAsync(databaseContext, workflowAppId, agentAppIds, cancellationToken);
        if (circular)
        {
            throw new BusinessException($"检测到循环嵌套（流程与 Agent 应用互为节点/工具）：{chain}。请调整流程编排或 Agent 应用的流程工具绑定.") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 判断引入指定 Agent 应用是否与当前流程构成循环（供可选项查询标记）.
    /// </summary>
    public static async Task<bool> IsCircularAsync(DatabaseContext databaseContext, Guid workflowAppId, Guid agentAppId, CancellationToken cancellationToken)
    {
        if (workflowAppId == Guid.Empty || agentAppId == Guid.Empty)
        {
            return false;
        }

        var (circular, _) = await FindCycleAsync(databaseContext, workflowAppId, [agentAppId], cancellationToken);
        return circular;
    }

    /// <summary>
    /// BFS 传递闭包：从起点 Agent 出发，经「Agent 的流程工具 → 流程定义中的 agentApp 节点 → 其 Agent」展开，
    /// 任何一条路径回到 workflowAppId 即成环并返回链路描述.
    /// </summary>
    private static async Task<(bool Circular, string Chain)> FindCycleAsync(DatabaseContext databaseContext, Guid workflowAppId, IReadOnlyCollection<Guid> agentAppIds, CancellationToken cancellationToken)
    {
        var visitedAgents = new HashSet<Guid>();
        var visitedWorkflows = new HashSet<Guid>();
        var appNames = new Dictionary<Guid, string>();

        string Label(Guid appId) => appNames.TryGetValue(appId, out var name) && !string.IsNullOrWhiteSpace(name) ? $"「{name}」" : appId.ToString("N")[..8];

        // 起点 Agent 名称（链路文案用）
        await LoadAppNamesAsync(databaseContext, agentAppIds.Append(workflowAppId), appNames, cancellationToken);

        var queue = new Queue<(Guid AgentId, string Chain)>();
        foreach (var agentId in agentAppIds.Distinct())
        {
            if (visitedAgents.Add(agentId))
            {
                queue.Enqueue((agentId, $"本流程 → Agent {Label(agentId)}"));
            }
        }

        while (queue.Count > 0)
        {
            var (agentId, chain) = queue.Dequeue();

            foreach (var toolAppId in await LoadAgentWorkflowToolsAsync(databaseContext, agentId, appNames, cancellationToken))
            {
                if (toolAppId == workflowAppId)
                {
                    return (true, $"{chain} → 流程工具 {Label(toolAppId)}（回到本流程）");
                }

                if (!visitedWorkflows.Add(toolAppId))
                {
                    continue;
                }

                foreach (var nextAgentId in await LoadWorkflowAgentNodesAsync(databaseContext, toolAppId, appNames, cancellationToken))
                {
                    if (visitedAgents.Add(nextAgentId))
                    {
                        queue.Enqueue((nextAgentId, $"{chain} → 流程工具 {Label(toolAppId)} → Agent {Label(nextAgentId)}"));
                    }
                }
            }
        }

        return (false, string.Empty);
    }

    /// <summary>加载 Agent 应用生效配置（发布快照优先）中绑定的流程工具 id 集合.</summary>
    private static async Task<IReadOnlyList<Guid>> LoadAgentWorkflowToolsAsync(DatabaseContext databaseContext, Guid agentAppId, Dictionary<Guid, string> appNames, CancellationToken cancellationToken)
    {
        var app = await databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == agentAppId)
            .Select(x => new { x.Id, x.Name, x.AppType, x.PublishStatus })
            .FirstOrDefaultAsync(cancellationToken);
        if (app == null || app.AppType != (int)MoAI.Database.Enums.AppType.Agent)
        {
            return [];
        }

        appNames[app.Id] = app.Name ?? string.Empty;

        var config = await databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == agentAppId)
            .Select(x => new { x.WorkflowApps, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            return [];
        }

        // 发布快照优先（与运行时工具装配一致）；未发布/快照非法回退草稿行
        var entity = new MoAI.Database.Entities.AppAgentConfigEntity { AppId = agentAppId, WorkflowApps = config.WorkflowApps, PublishedConfig = config.PublishedConfig };
        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(
            new MoAI.Database.Entities.AppEntity { Id = agentAppId, PublishStatus = app.PublishStatus },
            entity,
            preferPublished: true);

        return ParseGuidList(effective.WorkflowApps);
    }

    /// <summary>加载流程应用发布快照定义中 agentApp 节点引用的 Agent id 集合（未发布视为无工具边）.</summary>
    private static async Task<IReadOnlyList<Guid>> LoadWorkflowAgentNodesAsync(DatabaseContext databaseContext, Guid workflowAppId, Dictionary<Guid, string> appNames, CancellationToken cancellationToken)
    {
        var definitionJson = await databaseContext.AppWorkflowConfigs.AsNoTracking()
            .Where(x => x.AppId == workflowAppId)
            .Select(x => x.PublishedDefinition)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return [];
        }

        WorkflowDefinition? definition;
        try
        {
            definition = WorkflowJson.DeserializeDefinition(definitionJson);
        }
        catch (JsonException)
        {
            return [];
        }

        var ids = CollectAgentAppIds(definition);
        if (ids.Count > 0)
        {
            await LoadAppNamesAsync(databaseContext, ids, appNames, cancellationToken);
        }

        return ids.ToList();
    }

    private static async Task LoadAppNamesAsync(DatabaseContext databaseContext, IEnumerable<Guid> appIds, Dictionary<Guid, string> appNames, CancellationToken cancellationToken)
    {
        var missing = appIds.Where(id => id != Guid.Empty && !appNames.ContainsKey(id)).Distinct().ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var rows = await databaseContext.Apps.AsNoTracking()
            .Where(x => missing.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            appNames[row.Id] = row.Name ?? string.Empty;
        }
    }

    /// <summary>解析 JSON 字符串数组的 Guid 列表（非法/空值过滤）.</summary>
    internal static IReadOnlyList<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            var result = new List<Guid>();
            foreach (var item in raw)
            {
                if (Guid.TryParse(item, out var id) && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
