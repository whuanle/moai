using System.Text.Json;

namespace MoAI.AI;

/// <summary>
/// 工具审批策略：<c>app_agent_config.execution_settings</c> JSON 的 <c>toolApproval</c> 节
/// （<c>{ "autoApprovePlugins": ["&lt;pluginId&gt;"], "autoApproveWorkflowApps": [], "autoApproveAgentApps": [],
/// "autoApproveSkills": [], "autoApproveWikis": [1], "autoApproveGraphs": [2], "sandboxAutoApproved": true }</c>）.
/// 审批模式（approval）下按各绑定资源的白名单与沙箱开关自动放行部分工具，其余工具仍挂起等待人工批准；
/// 由 AI.Core（闸口放行）与 App 模块（策略校验/工具名下发）共用，JSON 形状变更须两端同步.
/// </summary>
public sealed class AppToolApprovalPolicy
{
    /// <summary>
    /// 空策略：不自动放行任何工具（未配置 toolApproval 节时的行为，与历史一致）.
    /// </summary>
    public static AppToolApprovalPolicy Empty { get; } = new();

    /// <summary>
    /// 沙箱工具自动放行（kind=sandbox：运行代码/Shell/文件读写等）；
    /// 关闭时审批模式下每次沙箱调用都需人工批准.
    /// </summary>
    public bool SandboxAutoApproved { get; init; }

    /// <summary>
    /// 自动放行的插件 id 白名单（plugin.id）；命中插件产出的工具（静态/动态/MCP/OpenAPI）直接执行.
    /// </summary>
    public IReadOnlyList<Guid> AutoApprovePlugins { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 自动放行的流程应用 id 白名单（app.id，kind=workflow）；命中应用产出的流程工具直接执行.
    /// </summary>
    public IReadOnlyList<Guid> AutoApproveWorkflowApps { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 自动放行的 Agent 应用 id 白名单（app.id，kind=agentapp）；命中应用产出的 agent__ 工具直接执行.
    /// </summary>
    public IReadOnlyList<Guid> AutoApproveAgentApps { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 自动放行的技能 id 白名单（skill.id，kind=skill）；命中技能的 skill_* 装载工具直接执行.
    /// </summary>
    public IReadOnlyList<Guid> AutoApproveSkills { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 自动放行的知识库 id 白名单（wiki.id，kind=wiki）；检索工具一次调用覆盖全部绑定知识库，
    /// 须全部绑定知识库均命中白名单才自动放行，任一未命中即挂起等待人工批准.
    /// </summary>
    public IReadOnlyList<long> AutoApproveWikis { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 自动放行的知识图谱 id 白名单（knowledge_graph.id，kind=graph）；检索工具一次调用覆盖全部绑定图谱，
    /// 须全部绑定图谱均命中白名单才自动放行，任一未命中即挂起等待人工批准.
    /// </summary>
    public IReadOnlyList<long> AutoApproveGraphs { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 从执行参数 JSON 文本解析审批策略；空/非法/无 toolApproval 节返回空策略.
    /// </summary>
    /// <param name="executionSettingsJson">execution_settings JSON 文本.</param>
    /// <returns>审批策略.</returns>
    public static AppToolApprovalPolicy Parse(string? executionSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(executionSettingsJson))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(executionSettingsJson);
            return FromRootElement(document.RootElement);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>
    /// 从执行参数 JSON 根对象解析审批策略.
    /// </summary>
    /// <param name="root">执行参数 JSON 根对象.</param>
    /// <returns>审批策略.</returns>
    public static AppToolApprovalPolicy FromRootElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("toolApproval", out var node)
            || node.ValueKind != JsonValueKind.Object)
        {
            return Empty;
        }

        var plugins = ParseGuidList(node, "autoApprovePlugins");
        var workflowApps = ParseGuidList(node, "autoApproveWorkflowApps");
        var agentApps = ParseGuidList(node, "autoApproveAgentApps");
        var skills = ParseGuidList(node, "autoApproveSkills");
        var wikis = ParseLongList(node, "autoApproveWikis");
        var graphs = ParseLongList(node, "autoApproveGraphs");

        var sandboxAutoApproved = node.TryGetProperty("sandboxAutoApproved", out var flag)
            && flag.ValueKind == JsonValueKind.True;

        return new AppToolApprovalPolicy
        {
            SandboxAutoApproved = sandboxAutoApproved,
            AutoApprovePlugins = plugins.Distinct().ToList(),
            AutoApproveWorkflowApps = workflowApps.Distinct().ToList(),
            AutoApproveAgentApps = agentApps.Distinct().ToList(),
            AutoApproveSkills = skills.Distinct().ToList(),
            AutoApproveWikis = wikis.Distinct().ToList(),
            AutoApproveGraphs = graphs.Distinct().ToList(),
        };
    }

    private static List<Guid> ParseGuidList(JsonElement node, string property)
    {
        var result = new List<Guid>();
        if (node.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && Guid.TryParse(item.GetString(), out var id)
                    && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }
        }

        return result;
    }

    private static List<long> ParseLongList(JsonElement node, string property)
    {
        var result = new List<long>();
        if (node.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number
                    && item.TryGetInt64(out var id)
                    && id > 0)
                {
                    result.Add(id);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 判断工具在审批模式下是否按策略自动放行：沙箱开关命中沙箱类工具；
    /// 单源工具（插件/流程应用/Agent 应用/技能）按来源 id 命中各自白名单；
    /// 多源检索工具（知识库/知识图谱）须其覆盖的全部来源 id 均命中白名单.
    /// </summary>
    /// <param name="toolKind">工具来源类型（sandbox/static/dynamic/mcp/openapi/workflow/agentapp/skill/wiki/graph）.</param>
    /// <param name="sourceId">工具来源资源 id（插件/应用/技能 id），无来源为 null.</param>
    /// <param name="sourceLongIds">多源工具覆盖的资源 id 列表（知识库/图谱 id），单源工具为 null.</param>
    /// <returns>自动放行返回 true.</returns>
    public bool IsAutoApproved(string? toolKind, Guid? sourceId, IReadOnlyList<long>? sourceLongIds = null)
        => toolKind switch
        {
            AppToolApprovalContract.KindSandbox => SandboxAutoApproved,
            "static" or "dynamic" or "mcp" or "openapi" => sourceId.HasValue && AutoApprovePlugins.Contains(sourceId.Value),
            "workflow" => sourceId.HasValue && AutoApproveWorkflowApps.Contains(sourceId.Value),
            "agentapp" => sourceId.HasValue && AutoApproveAgentApps.Contains(sourceId.Value),
            "skill" => sourceId.HasValue && AutoApproveSkills.Contains(sourceId.Value),
            "wiki" => IsAllSourcesApproved(sourceLongIds, AutoApproveWikis),
            "graph" => IsAllSourcesApproved(sourceLongIds, AutoApproveGraphs),
            _ => false,
        };

    private static bool IsAllSourcesApproved(IReadOnlyList<long>? sourceLongIds, IReadOnlyList<long> whitelist)
        => sourceLongIds is { Count: > 0 } && sourceLongIds.All(whitelist.Contains);
}

/// <summary>
/// 插件工具命名契约：MCP/OpenAPI 自定义插件的函数工具名为 <c>{插件名}__{函数名}</c>，
/// 闸口（AppTool.SourceId 白名单）与用户配置下发（工具名列表）须按同一规则拼装.
/// </summary>
public static class AppPluginToolNaming
{
    /// <summary>
    /// 拼装自定义插件（MCP/OpenAPI）的函数工具名.
    /// </summary>
    /// <param name="pluginName">插件名（plugin.plugin_name）.</param>
    /// <param name="functionName">函数名（plugin_function.name）.</param>
    /// <returns>工具名.</returns>
    public static string FunctionToolName(string pluginName, string functionName) => $"{pluginName}__{functionName}";
}

/// <summary>
/// 应用类资源（流程/Agent 应用）工具命名契约：工具名为 <c>{前缀}{应用名}</c>，
/// 同名应用加短 id 后缀避免冲突；闸口装配与用户配置下发（自动放行工具名）须按同一规则拼装.
/// </summary>
public static class AppResourceToolNaming
{
    /// <summary>
    /// 按装配去重规则生成带前缀的工具名：首个直接用 <c>{prefix}{name}</c>，
    /// 同名冲突时追加 <c>_{id 前 8 位}</c>，短 id 仍冲突返回空串（上游丢弃）.
    /// </summary>
    /// <param name="prefix">工具名前缀（workflow__/agent__）.</param>
    /// <param name="name">应用名.</param>
    /// <param name="id">应用 id.</param>
    /// <param name="usedNames">已占用工具名集合（调用侧按绑定顺序传入并持续累加）.</param>
    /// <returns>工具名；短 id 仍冲突返回空串.</returns>
    public static string PrefixedToolName(string prefix, string name, Guid id, HashSet<string> usedNames)
    {
        var toolName = prefix + name;
        if (usedNames.Add(toolName))
        {
            return toolName;
        }

        toolName = $"{toolName}_{id.ToString("N")[..8]}";
        return usedNames.Add(toolName) ? toolName : string.Empty;
    }
}
