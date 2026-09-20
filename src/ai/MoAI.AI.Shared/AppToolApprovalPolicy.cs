using System.Text.Json;

namespace MoAI.AI;

/// <summary>
/// 工具审批策略：<c>app_agent_config.execution_settings</c> JSON 的 <c>toolApproval</c> 节
/// （<c>{ "autoApprovePlugins": ["&lt;pluginId&gt;"], "sandboxAutoApproved": true }</c>）.
/// 审批模式（approval）下按插件白名单与沙箱开关自动放行部分工具，其余工具仍挂起等待人工批准；
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

        var plugins = new List<Guid>();
        if (node.TryGetProperty("autoApprovePlugins", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && Guid.TryParse(item.GetString(), out var id)
                    && id != Guid.Empty)
                {
                    plugins.Add(id);
                }
            }
        }

        var sandboxAutoApproved = node.TryGetProperty("sandboxAutoApproved", out var flag)
            && flag.ValueKind == JsonValueKind.True;

        return new AppToolApprovalPolicy
        {
            SandboxAutoApproved = sandboxAutoApproved,
            AutoApprovePlugins = plugins.Distinct().ToList(),
        };
    }

    /// <summary>
    /// 判断工具在审批模式下是否按策略自动放行：沙箱开关命中沙箱类工具，或工具来源（插件/流程应用 id）在白名单内.
    /// </summary>
    /// <param name="toolKind">工具来源类型（sandbox/static/dynamic/mcp/openapi/workflow）.</param>
    /// <param name="sourceId">工具来源资源 id（插件/流程应用 id），无来源为 null.</param>
    /// <returns>自动放行返回 true.</returns>
    public bool IsAutoApproved(string? toolKind, Guid? sourceId)
        => (SandboxAutoApproved && toolKind == AppToolApprovalContract.KindSandbox)
            || (sourceId.HasValue && AutoApprovePlugins.Contains(sourceId.Value));
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
