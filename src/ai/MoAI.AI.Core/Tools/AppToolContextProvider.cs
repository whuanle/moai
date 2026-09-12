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
/// </summary>
public sealed class AppToolContextProvider : AIContextProvider
{
    private const string ListToolName = "list_tools";
    private const string CallToolName = "call_tool";

    private readonly IReadOnlyList<AppTool> _tools;
    private readonly AITool[] _metaTools;
    private readonly string? _instructions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppToolContextProvider"/> class.
    /// </summary>
    /// <param name="tools">当前应用可用工具.</param>
    public AppToolContextProvider(IReadOnlyList<AppTool> tools)
    {
        _tools = tools;

        // 使用本地函数以便为元工具参数提供默认值（可选参数），并让 MEAI 生成参数 Schema.
        async Task<string> ListTools(string? query = null) => await BuildListJsonAsync(query).ConfigureAwait(false);
        async Task<string> CallTool(string toolName, string? argumentsJson = null) => await InvokeJsonAsync(toolName, argumentsJson).ConfigureAwait(false);

        _metaTools =
        [
            AIFunctionFactory.Create(
                ListTools,
                name: ListToolName,
                description: "查看当前应用可用的工具（插件与知识库）。当需要外部能力（搜索、查询、数据库、业务接口等）时，先调用本工具获取工具名称、说明与参数示例；可传入 query 关键字做筛选。"),
            AIFunctionFactory.Create(
                CallTool,
                name: CallToolName,
                description: "调用 list_tools 返回的工具。toolName 为工具名称；argumentsJson 为 JSON 字符串参数，字段参考 list_tools 返回的 parametersExample。"),
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
你可以调用当前应用绑定的工具（插件/知识库）来获取外部信息或执行操作：
1. 先用 list_tools 查看可用工具及其参数示例（可按关键字筛选）。
2. 再用 call_tool(toolName, argumentsJson) 调用目标工具。
已绑定的工具：
{string.Join('\n', names)}{more}
""";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
