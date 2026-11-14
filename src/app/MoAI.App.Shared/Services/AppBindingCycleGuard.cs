using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Services;

/// <summary>
/// 应用绑定循环嵌套守卫（Agent ↔ 流程互调有向图）：边有三类——Agent 的 Agent 应用工具
/// （app_agent_config.agent_apps）、Agent 的流程应用工具（app_agent_config.workflow_apps）、
/// 流程定义的 agentApp 节点。本守卫从根应用出发沿边做传递闭包检测，任一路径引回根即视为循环：
/// 流程侧在保存草稿/发布/调试执行/运行期节点调用时拦截，Agent 侧在保存配置时以草稿全量拦截.
/// </summary>
/// <remarks>
/// 位于 App.Shared 以便 App.Core（Agent 配置保存校验）与工作流模块共用；流程定义解析用
/// JsonDocument 直读（camelCase 契约：nodes[].type / nodes[].config.agentAppId），不依赖工作流引擎模型.
/// </remarks>
public static class AppBindingCycleGuard
{
    /// <summary>流程定义 agentApp 节点类型常量（与引擎 NodeTypes.AgentApp 一致，此处不引引擎）.</summary>
    private const string AgentAppNodeType = "agentApp";

    /// <summary>
    /// 校验流程应用引用的 Agent 应用闭包不会引回当前流程应用，否则抛出 400.
    /// </summary>
    public static async Task EnsureNoCycleFromWorkflowAsync(DatabaseContext databaseContext, Guid workflowAppId, IReadOnlyCollection<Guid> agentAppIds, CancellationToken cancellationToken)
    {
        if (workflowAppId == Guid.Empty || agentAppIds.Count == 0)
        {
            return;
        }

        var (circular, chain) = await FindCycleAsync(databaseContext, workflowAppId, rootIsWorkflow: true, agentAppIds, [], cancellationToken).ConfigureAwait(false);
        if (circular)
        {
            throw new BusinessException($"检测到循环嵌套（流程与 Agent 应用互为节点/工具）：{chain}。请调整流程编排或 Agent 应用的流程工具绑定.") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 判断流程引入指定 Agent 应用是否构成循环（供可选项查询标记）.
    /// </summary>
    public static async Task<bool> IsCircularFromWorkflowAsync(DatabaseContext databaseContext, Guid workflowAppId, Guid agentAppId, CancellationToken cancellationToken)
    {
        if (workflowAppId == Guid.Empty || agentAppId == Guid.Empty)
        {
            return false;
        }

        var (circular, _) = await FindCycleAsync(databaseContext, workflowAppId, rootIsWorkflow: true, [agentAppId], [], cancellationToken).ConfigureAwait(false);
        return circular;
    }

    /// <summary>
    /// 校验 Agent 应用保存的 Agent 应用工具绑定闭包不会经 agent_apps 边引回自身，否则抛出 400；
    /// boundAgentAppIds 须为本次保存后的草稿全量（含保持不变的既有绑定）.
    /// </summary>
    /// <remarks>
    /// 只沿 Agent→Agent（agent_apps）边做闭包：Agent 绑定「含自身 agentApp 节点的流程」是平台既有语义
    /// （WF-26），该类环由流程侧保存/发布/调试守卫与运行期根流程守卫拦截，Agent 侧保存不拒绝.
    /// </remarks>
    public static async Task EnsureNoCycleFromAgentAsync(DatabaseContext databaseContext, Guid agentAppId, IReadOnlyCollection<Guid> boundAgentAppIds, CancellationToken cancellationToken)
    {
        if (agentAppId == Guid.Empty || boundAgentAppIds.Count == 0)
        {
            return;
        }

        if (boundAgentAppIds.Contains(agentAppId))
        {
            throw new BusinessException("Agent 应用不能绑定自身作为工具.") { StatusCode = 400 };
        }

        var (circular, chain) = await FindAgentCycleAsync(databaseContext, agentAppId, boundAgentAppIds, cancellationToken).ConfigureAwait(false);
        if (circular)
        {
            throw new BusinessException($"检测到循环嵌套（Agent 应用互为工具）：{chain}。请调整 Agent 应用工具绑定.") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 从流程定义 JSON 直读 agentApp 节点引用的 Agent 应用 id 集合（非法/空值过滤）.
    /// </summary>
    public static IReadOnlyCollection<Guid> CollectAgentAppIdsFromJson(string? definitionJson)
    {
        var ids = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return ids;
        }

        try
        {
            using var document = JsonDocument.Parse(definitionJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var node in nodes.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object
                    || !node.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || !string.Equals(type.GetString(), AgentAppNodeType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (node.TryGetProperty("config", out var config)
                    && config.ValueKind == JsonValueKind.Object
                    && config.TryGetProperty("agentAppId", out var element)
                    && element.ValueKind == JsonValueKind.String
                    && Guid.TryParse(element.GetString(), out var id)
                    && id != Guid.Empty)
                {
                    ids.Add(id);
                }
            }
        }
        catch (JsonException)
        {
            // 非法定义视为无边（与引擎反序列化失败的兜底一致）
        }

        return ids;
    }

    /// <summary>
    /// BFS 传递闭包：从根应用的出边出发，沿「Agent 的工具绑定 → Agent/流程」「流程的 agentApp 节点 → Agent」展开，
    /// 任何一条路径回到根应用即成环并返回链路描述；rootIsWorkflow 决定起点边与链路文案的根称谓.
    /// </summary>
    private static async Task<(bool Circular, string Chain)> FindCycleAsync(DatabaseContext databaseContext, Guid rootAppId, bool rootIsWorkflow, IReadOnlyCollection<Guid> startAgentIds, IReadOnlyCollection<Guid> startWorkflowIds, CancellationToken cancellationToken)
    {
        var visitedAgents = new HashSet<Guid>();
        var visitedWorkflows = new HashSet<Guid>();
        var appNames = new Dictionary<Guid, string>();
        var rootLabel = rootIsWorkflow ? "本流程" : "本应用";

        string Label(Guid appId) => appNames.TryGetValue(appId, out var name) && !string.IsNullOrWhiteSpace(name) ? $"「{name}」" : appId.ToString("N")[..8];

        // 起点名（链路文案用）；流程根的起点边是 agentApp 节点，Agent 根的起点边是工具绑定
        var startEdgeLabel = rootIsWorkflow ? "Agent 节点" : "Agent 工具";
        await LoadAppNamesAsync(databaseContext, startAgentIds.Concat(startWorkflowIds).Append(rootAppId), appNames, cancellationToken).ConfigureAwait(false);

        var queue = new Queue<(bool IsWorkflow, Guid Id, string Chain)>();
        foreach (var id in startAgentIds.Distinct())
        {
            if (visitedAgents.Add(id))
            {
                queue.Enqueue((false, id, $"{rootLabel} → {startEdgeLabel} {Label(id)}"));
            }
        }

        foreach (var id in startWorkflowIds.Distinct())
        {
            if (visitedWorkflows.Add(id))
            {
                queue.Enqueue((true, id, $"{rootLabel} → 流程工具 {Label(id)}"));
            }
        }

        while (queue.Count > 0)
        {
            var (isWorkflow, id, chain) = queue.Dequeue();

            if (isWorkflow)
            {
                foreach (var nextAgentId in await LoadWorkflowAgentNodesAsync(databaseContext, id, appNames, cancellationToken).ConfigureAwait(false))
                {
                    if (nextAgentId == rootAppId)
                    {
                        return (true, $"{chain} → Agent 节点 {Label(nextAgentId)}（回到{rootLabel}）");
                    }

                    if (visitedAgents.Add(nextAgentId))
                    {
                        queue.Enqueue((false, nextAgentId, $"{chain} → Agent 节点 {Label(nextAgentId)}"));
                    }
                }
            }
            else
            {
                var (agentTools, workflowTools) = await LoadAgentToolEdgesAsync(databaseContext, id, appNames, cancellationToken).ConfigureAwait(false);
                foreach (var target in agentTools)
                {
                    if (target == rootAppId)
                    {
                        return (true, $"{chain} → Agent 工具 {Label(target)}（回到{rootLabel}）");
                    }

                    if (visitedAgents.Add(target))
                    {
                        queue.Enqueue((false, target, $"{chain} → Agent 工具 {Label(target)}"));
                    }
                }

                foreach (var target in workflowTools)
                {
                    if (target == rootAppId)
                    {
                        return (true, $"{chain} → 流程工具 {Label(target)}（回到{rootLabel}）");
                    }

                    if (visitedWorkflows.Add(target))
                    {
                        queue.Enqueue((true, target, $"{chain} → 流程工具 {Label(target)}"));
                    }
                }
            }
        }

        return (false, string.Empty);
    }

    /// <summary>
    /// Agent 根闭包检测：从根的 Agent 工具绑定出发，仅沿 Agent→Agent（agent_apps）边展开，
    /// 任何一条路径回到根即成环（流程边不参与，见 <see cref="EnsureNoCycleFromAgentAsync"/> 备注）.
    /// </summary>
    private static async Task<(bool Circular, string Chain)> FindAgentCycleAsync(DatabaseContext databaseContext, Guid rootAppId, IReadOnlyCollection<Guid> startAgentIds, CancellationToken cancellationToken)
    {
        var visitedAgents = new HashSet<Guid>();
        var appNames = new Dictionary<Guid, string>();

        string Label(Guid appId) => appNames.TryGetValue(appId, out var name) && !string.IsNullOrWhiteSpace(name) ? $"「{name}」" : appId.ToString("N")[..8];

        await LoadAppNamesAsync(databaseContext, startAgentIds.Append(rootAppId), appNames, cancellationToken).ConfigureAwait(false);

        var queue = new Queue<(Guid Id, string Chain)>();
        foreach (var id in startAgentIds.Distinct())
        {
            if (visitedAgents.Add(id))
            {
                queue.Enqueue((id, $"本应用 → Agent 工具 {Label(id)}"));
            }
        }

        while (queue.Count > 0)
        {
            var (id, chain) = queue.Dequeue();
            var (agentTools, _) = await LoadAgentToolEdgesAsync(databaseContext, id, appNames, cancellationToken).ConfigureAwait(false);
            foreach (var target in agentTools)
            {
                if (target == rootAppId)
                {
                    return (true, $"{chain} → Agent 工具 {Label(target)}（回到本应用）");
                }

                if (visitedAgents.Add(target))
                {
                    queue.Enqueue((target, $"{chain} → Agent 工具 {Label(target)}"));
                }
            }
        }

        return (false, string.Empty);
    }

    /// <summary>加载 Agent 应用生效配置（发布快照优先）中绑定的 Agent 工具与流程工具 id 集合.</summary>
    private static async Task<(IReadOnlyList<Guid> AgentTools, IReadOnlyList<Guid> WorkflowTools)> LoadAgentToolEdgesAsync(DatabaseContext databaseContext, Guid agentAppId, Dictionary<Guid, string> appNames, CancellationToken cancellationToken)
    {
        var app = await databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == agentAppId)
            .Select(x => new { x.Id, x.Name, x.AppType, x.PublishStatus })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (app == null || app.AppType != (int)AppType.Agent)
        {
            return ([], []);
        }

        appNames[app.Id] = app.Name ?? string.Empty;

        var config = await databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == agentAppId)
            .Select(x => new { x.WorkflowApps, x.AgentApps, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (config == null)
        {
            return ([], []);
        }

        // 发布快照优先（与运行时工具装配一致）；未发布/快照非法回退草稿行
        var entity = new AppAgentConfigEntity { AppId = agentAppId, WorkflowApps = config.WorkflowApps, AgentApps = config.AgentApps, PublishedConfig = config.PublishedConfig };
        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(
            new AppEntity { Id = agentAppId, PublishStatus = app.PublishStatus },
            entity,
            preferPublished: true);

        return (ParseGuidList(effective.AgentApps), ParseGuidList(effective.WorkflowApps));
    }

    /// <summary>加载流程应用发布快照定义中 agentApp 节点引用的 Agent id 集合（未发布视为无边）.</summary>
    private static async Task<IReadOnlyList<Guid>> LoadWorkflowAgentNodesAsync(DatabaseContext databaseContext, Guid workflowAppId, Dictionary<Guid, string> appNames, CancellationToken cancellationToken)
    {
        var definitionJson = await databaseContext.AppWorkflowConfigs.AsNoTracking()
            .Where(x => x.AppId == workflowAppId)
            .Select(x => x.PublishedDefinition)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        var ids = CollectAgentAppIdsFromJson(definitionJson);
        if (ids.Count > 0)
        {
            await LoadAppNamesAsync(databaseContext, ids, appNames, cancellationToken).ConfigureAwait(false);
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
            .ToListAsync(cancellationToken).ConfigureAwait(false);
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
