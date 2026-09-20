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

    private readonly IReadOnlyList<AppTool> _tools;
    private readonly AITool[] _metaTools;
    private readonly string? _instructions;
    private readonly AppToolApprovalService? _approvalService;
    private readonly AppToolApprovalGate? _approvalGate;

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

        // 使用本地函数以便为元工具参数提供默认值（可选参数），并让 MEAI 生成参数 Schema.
        // 注意：参数类型必须容忍模型传对象/数组（部分模型不遵守 schema 的 string 约定，
        // string 形参会在参数编组时抛 JsonException 中断整轮对话），故用 JsonNode 承接后自行归一.
        async Task<string> ListTools(System.Text.Json.Nodes.JsonNode? query = null) => await BuildListJsonAsync(ToJsonString(query)).ConfigureAwait(false);
        async Task<string> CallTool(System.Text.Json.Nodes.JsonNode? toolName, System.Text.Json.Nodes.JsonNode? argumentsJson = null) => await InvokeJsonAsync(ToJsonString(toolName), ToJsonString(argumentsJson)).ConfigureAwait(false);

        _metaTools =
        [
            AIFunctionFactory.Create(
                ListTools,
                name: ListToolName,
                description: "查看当前应用可用的工具（插件、知识库与流程应用）。当需要外部能力（搜索、查询、数据库、业务接口等）时，先调用本工具获取工具名称、说明与参数示例；可传入 query 关键字做筛选。"),
            AIFunctionFactory.Create(
                CallTool,
                name: CallToolName,
                description: "调用 list_tools 返回的工具。toolName 为工具名称字符串；argumentsJson 为工具的 JSON 参数对象（如 {\"code\":\"print(1)\"}），字段参考 list_tools 返回的 parametersExample。"),
        ];

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
                description = tool.Description,
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

        // 审批模式：重要工具（沙箱/插件等有外部副作用）挂起等待人工批准/拒绝；
        // 审批策略（应用配置）白名单内的插件与沙箱自动放行，无需等待
        if (_approvalService != null
            && _approvalGate != null
            && _approvalGate.Mode == AppToolApprovalContract.ModeApproval
            && AppToolApprovalContract.KindRequiresApproval(tool.Kind)
            && !_approvalGate.Policy.IsAutoApproved(tool.Kind, tool.SourceId))
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

    private static string? BuildInstructions(IReadOnlyList<AppTool> tools)
    {
        if (tools.Count == 0)
        {
            return null;
        }

        const int maxListed = 30;
        var names = tools.Take(maxListed).Select(t => $"- {t.Name}：{t.Title}");
        var more = tools.Count > maxListed ? $"\n（另有 {tools.Count - maxListed} 个工具，可用 list_tools 查看）" : string.Empty;

        return $"""
## 工具调用
            你可以调用当前应用绑定的工具（插件/知识库/流程应用）来获取外部信息或执行操作：
1. 先用 list_tools 查看可用工具及其参数示例（可按关键字筛选）。
2. 再用 call_tool(toolName, argumentsJson) 调用目标工具。
已绑定的工具：
{string.Join('\n', names)}{more}
""";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
