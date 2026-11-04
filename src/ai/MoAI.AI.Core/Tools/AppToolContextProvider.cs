using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 工具上下文提供者：不把全部工具注入每轮请求，而是暴露 <c>list_tools</c>（按需加载工具列表）
/// 与 <c>call_tool</c>（按名称调用）两个元工具，实现渐进式工具披露.
/// 审批模式（approval）下，重要工具在 <c>call_tool</c> 执行前挂起等待人工决策.
/// </summary>
public sealed class AppToolContextProvider : AIContextProvider
{
    private const string ListToolName = "list_tools";
    private const string CallToolName = "call_tool";
    private const string PluginNoteToolName = "plugin_note";

    private readonly IReadOnlyList<AppTool> _tools;
    private readonly AITool[] _metaTools;
    private readonly string? _instructions;
    private readonly AppToolApprovalService? _approvalService;
    private readonly AppToolApprovalGate? _approvalGate;

    /// <summary>
    /// 聚合后的插件按需语义条目（按 id 去重），供 <c>plugin_note</c> 取回正文.
    /// </summary>
    private readonly IReadOnlyList<AppPluginSemanticNote> _semanticNotes;

    /// <summary>
    /// 聚合后的插件级使用须知（同一插件的工具共享一份文本，按文本去重）.
    /// </summary>
    private readonly IReadOnlyList<(string Title, string Notes)> _usageNotes;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppToolContextProvider"/> class.
    /// </summary>
    /// <param name="tools">当前应用可用工具.</param>
    /// <param name="approvalService">工具审批服务，自动模式下为 null.</param>
    /// <param name="approvalGate">审批会话上下文，自动模式下为 null.</param>
    public AppToolContextProvider(IReadOnlyList<AppTool> tools, AppToolApprovalService? approvalService = null, AppToolApprovalGate? approvalGate = null)
    {
        _tools = tools;
        _approvalService = approvalService;
        _approvalGate = approvalGate;

        _semanticNotes = tools
            .SelectMany(t => t.SemanticNotes ?? [])
            .DistinctBy(n => n.Id)
            .ToList();

        _usageNotes = tools
            .Where(t => !string.IsNullOrWhiteSpace(t.UsageNotes))
            .GroupBy(t => t.UsageNotes!.Trim())
            .Select(g => (Title: g.First().Title, Notes: g.Key))
            .ToList();

        // 使用本地函数以便为元工具参数提供默认值（可选参数），并让 MEAI 生成参数 Schema.
        // 注意：参数类型必须容忍模型传对象/数组（部分模型不遵守 schema 的 string 约定，
        // string 形参会在参数编组时抛 JsonException 中断整轮对话），故用 JsonNode 承接后自行归一.
        async Task<string> ListTools(System.Text.Json.Nodes.JsonNode? query = null) => await BuildListJsonAsync(ToJsonString(query)).ConfigureAwait(false);
        async Task<string> CallTool(System.Text.Json.Nodes.JsonNode? toolName, System.Text.Json.Nodes.JsonNode? argumentsJson = null) => await InvokeJsonAsync(ToJsonString(toolName), ToJsonString(argumentsJson)).ConfigureAwait(false);
        async Task<string> PluginNote(System.Text.Json.Nodes.JsonNode? noteId = null) => await InvokePluginNoteAsync(ToJsonString(noteId)).ConfigureAwait(false);

        var metaTools = new List<AITool>
        {
            AIFunctionFactory.Create(
                ListTools,
                name: ListToolName,
                description: "查看当前应用可用的工具（插件、知识库与流程应用）。当需要外部能力（搜索、查询、数据库、业务接口等）时，先调用本工具获取工具名称、说明与参数示例；可传入 query 关键字做筛选。"),
            AIFunctionFactory.Create(
                CallTool,
                name: CallToolName,
                description: "调用 list_tools 返回的工具。toolName 为工具名称字符串；argumentsJson 为工具的 JSON 参数对象（如 {\"code\":\"print(1)\"}），字段参考 list_tools 返回的 parametersExample。"),
        };

        if (_semanticNotes.Count > 0)
        {
            metaTools.Add(AIFunctionFactory.Create(
                PluginNote,
                name: PluginNoteToolName,
                description: "获取插件附带的语义资料正文（如库表结构说明、业务术语、示例）。noteId 从系统提示的「语义资料目录」中获取，调用前不需要先用 list_tools。"));
        }

        _metaTools = [.. metaTools];

        _instructions = BuildInstructions(tools);
    }

    /// <inheritdoc/>
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        return new ValueTask<AIContext>(new AIContext
        {
            Tools = _metaTools,
            Instructions = _instructions,
        });
    }

    /// <summary>
    /// 获取装配好的元工具上下文（供集成测试校验 AIFunction 参数编组）.
    /// </summary>
    internal Task<AIContext> BuildAIContextAsync() => Task.FromResult(new AIContext
    {
        Tools = _metaTools,
        Instructions = _instructions,
    });

    internal async Task<string> BuildListJsonAsync(string? query)
    {
        var matched = _tools.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var keyword = query.Trim();
            matched = matched.Where(t =>
                t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                t.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var items = new List<object>();
        foreach (var tool in matched)
        {
            var example = tool.ParametersExample;
            if (example == null && tool.ResolveParametersExampleAsync != null)
            {
                try
                {
                    example = await tool.ResolveParametersExampleAsync(CancellationToken.None).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // 参数示例获取失败不影响列表
                catch (Exception)
                {
                    example = null;
                }
#pragma warning restore CA1031
            }

            items.Add(new
            {
                name = tool.Name,
                title = tool.Title,
                description = string.IsNullOrWhiteSpace(tool.ExtraNotes) ? tool.Description : $"{tool.Description}\n【使用说明】{tool.ExtraNotes}",
                kind = tool.Kind,
                parametersExample = example,
            });
        }

        return JsonSerializer.Serialize(new { count = items.Count, tools = items }, JsonOptions);
    }

    internal async Task<string> InvokeJsonAsync(string toolName, string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return ErrorJson("缺少 toolName.");
        }

        var tool = _tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            return ErrorJson($"工具不存在：{toolName}。请先调用 list_tools 查看可用工具。");
        }

        // 工具级调用策略预检（管理员语义限制）：denyPatterns 命中参数时按 action 处置
        var callPolicy = MoAI.AIPlugin.Models.PluginToolCallPolicy.Parse(tool.CallPolicy);
        if (callPolicy != null && callPolicy.IsActive && callPolicy.IsDenied(argumentsJson ?? string.Empty, out var matchedPattern))
        {
            if (callPolicy.Action == MoAI.AIPlugin.Models.PluginToolCallPolicy.ActionBlock
                || _approvalService == null
                || _approvalGate == null
                || _approvalGate.Mode != AppToolApprovalContract.ModeApproval)
            {
                return ErrorJson($"[插件调用策略] 该工具调用被管理员限制规则拦截（命中规则：{matchedPattern}）。请调整参数或改用其他方式，不要重复相同调用。");
            }

            // confirm：复用人工审批链，等待用户决策
            var policyDecision = await _approvalService.WaitDecisionAsync(_approvalGate, tool.Name, tool.Title, argumentsJson).ConfigureAwait(false);
            if (policyDecision != AppToolApprovalContract.StatusApproved)
            {
                return policyDecision == AppToolApprovalContract.StatusRejected
                    ? ErrorJson("用户拒绝了本次工具调用（命中插件调用策略），请勿重复尝试相同调用。")
                    : ErrorJson("工具调用等待人工审批超时（命中插件调用策略），本次未执行。");
            }
        }

        // 审批模式：纳入审批范围的工具（沙箱/插件/应用/技能/知识库与图谱检索等）挂起等待人工批准/拒绝；
        // 审批策略（应用配置）白名单命中的资源与沙箱开关自动放行，无需等待
        if (_approvalService != null
            && _approvalGate != null
            && _approvalGate.Mode == AppToolApprovalContract.ModeApproval
            && AppToolApprovalContract.KindRequiresApproval(tool.Kind)
            && !_approvalGate.Policy.IsAutoApproved(tool.Kind, tool.SourceId, tool.SourceLongIds))
        {
            var decision = await _approvalService.WaitDecisionAsync(_approvalGate, tool.Name, tool.Title, argumentsJson).ConfigureAwait(false);
            if (decision != AppToolApprovalContract.StatusApproved)
            {
                return decision == AppToolApprovalContract.StatusRejected
                    ? ErrorJson("用户拒绝了本次工具调用，请勿重复尝试相同调用，可向用户说明需要人工确认的原因。")
                    : ErrorJson("工具调用等待人工审批超时，本次未执行。");
            }
        }

        AppToolResult result;
        try
        {
            result = await tool.InvokeAsync(argumentsJson, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 调用异常统一转为工具错误文本，避免中断对话
        catch (Exception ex)
        {
            result = AppToolResult.Fail(ex.Message);
        }
#pragma warning restore CA1031

        return result.Success
            ? result.Data ?? "{\"success\":true}"
            : ErrorJson(result.Error ?? "工具调用失败.");
    }

    private static string ErrorJson(string message) => JsonSerializer.Serialize(new { success = false, error = message }, JsonOptions);

    /// <summary>
    /// plugin_note 元工具：按 noteId 取回语义条目正文.
    /// </summary>
    internal Task<string> InvokePluginNoteAsync(string? noteId)
    {
        if (!Guid.TryParse(noteId, out var id))
        {
            return Task.FromResult(ErrorJson("缺少或非法 noteId，请从系统提示的「语义资料目录」中复制。"));
        }

        var note = _semanticNotes.FirstOrDefault(n => n.Id == id);
        if (note == null)
        {
            return Task.FromResult(ErrorJson($"语义条目不存在：{noteId}。请查看系统提示中的目录。"));
        }

        return Task.FromResult(JsonSerializer.Serialize(new { title = note.Title, content = note.Content }, JsonOptions));
    }

    /// <summary>
    /// 模型实参归一化：字符串值取原文（兼容按 schema 传 JSON 字符串的模型），
    /// 其余形态（对象/数组/数字）序列化为紧凑 JSON 文本.
    /// </summary>
    private static string? ToJsonString(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        return node is System.Text.Json.Nodes.JsonValue value && value.TryGetValue(out string? text) ? text : node.ToJsonString();
    }

    private string? BuildInstructions(IReadOnlyList<AppTool> tools)
    {
        if (tools.Count == 0)
        {
            return null;
        }

        const int maxListed = 30;
        var names = tools.Take(maxListed).Select(t => $"- {t.Name}：{t.Title}");
        var more = tools.Count > maxListed ? $"\n（另有 {tools.Count - maxListed} 个工具，可用 list_tools 查看）" : string.Empty;

        var usageNotesSection = _usageNotes.Count > 0
            ? $"""

## 插件使用须知
以下为管理员维护的插件背景说明，调用相关工具前先阅读：
{string.Join("\n\n", _usageNotes.Select(x => $"【{x.Title}】\n{x.Notes}"))}
"""
            : string.Empty;

        var catalogSection = _semanticNotes.Count > 0
            ? $"""

## 语义资料目录
以下插件附有详细语义资料（如库表结构、字段用途、业务术语），需要时用 plugin_note(noteId) 取回正文：
{string.Join('\n', _semanticNotes.Select(n => $"- noteId={n.Id:N}：{n.Title}"))}
"""
            : string.Empty;

        return $"""
## 工具调用
            你可以调用当前应用绑定的工具（插件/知识库/流程应用）来获取外部信息或执行操作：
1. 先用 list_tools 查看可用工具及其参数示例（可按关键字筛选）。
2. 再用 call_tool(toolName, argumentsJson) 调用目标工具。
已绑定的工具：
{string.Join('\n', names)}{more}{usageNotesSection}{catalogSection}
""";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
