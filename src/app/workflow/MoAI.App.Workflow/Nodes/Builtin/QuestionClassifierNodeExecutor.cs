using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 问题分类节点执行器 - 通过 <see cref="IAiChatClient"/> 调用模型，把用户问题归入预定义分类之一.
/// 输入：query（必填，用户问题）、history（可选，[{role, content}] 聊天记录）.
/// 输出 = 输入透传 + { result: 命中分类 id, className: 分类名 }；调度器按 result 匹配出边的 condition 标记（分类 id）.
/// 模型输出无法解析时兜底命中第一个分类（保证流程继续）；config.aiModelId 为模型 id（ai_model.id）字符串.
/// config: { "aiModelId": "...", "backgroundKnowledge": "...", "historyCount": 6, "classes": [ { "id": "c1", "label": "售前咨询" } ] }
/// </summary>
public class QuestionClassifierNodeExecutor : INodeExecutor
{
    /// <summary>
    /// 聊天记录默认携带条数.
    /// </summary>
    public const int DefaultHistoryCount = 6;

    /// <summary>
    /// 聊天记录携带条数上限.
    /// </summary>
    public const int MaxHistoryCount = 50;

    private readonly IAiChatClient _aiChatClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionClassifierNodeExecutor"/> class.
    /// </summary>
    public QuestionClassifierNodeExecutor(IAiChatClient aiChatClient)
    {
        _aiChatClient = aiChatClient;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.QuestionClassifier;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var classes = ParseClasses(context.Config);
        if (classes.Count == 0)
        {
            return NodeExecutionResult.Failure("问题分类节点缺少配置：config.classes（至少需要配置一个分类）");
        }

        if (!context.Inputs.TryGetPropertyValue("query", out var queryNode) || queryNode == null)
        {
            return NodeExecutionResult.Failure("问题分类节点缺少必需的输入字段：query");
        }

        var query = queryNode is JsonValue ? queryNode.GetValue<string>() : queryNode.ToJsonString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return NodeExecutionResult.Failure("问题分类节点的输入字段 query（用户问题）不能为空");
        }

        var model = context.GetConfigString("aiModelId");
        if (string.IsNullOrWhiteSpace(model))
        {
            return NodeExecutionResult.Failure("问题分类节点未配置 AI 模型（config.aiModelId）");
        }

        // 聊天记录：只携带最近 N 条（默认 6、上限 50），截断副本避免重挂载上游输入
        JsonArray? history = null;
        if (context.Inputs.TryGetPropertyValue("history", out var historyNode) && historyNode is JsonArray historyArray)
        {
            var count = ParseHistoryCount(context.Config);
            var takeLast = historyArray.Skip(Math.Max(0, historyArray.Count - count)).ToList();
            history = new JsonArray(takeLast.Select(item => item?.DeepClone()).ToArray());
        }

        var systemPrompt = BuildSystemPrompt(classes, context.GetConfigString("backgroundKnowledge"));

        string answer;
        try
        {
            answer = await _aiChatClient.CompleteAsync(
                systemPrompt,
                query,
                history,
                model,
                async chunk => await context.ReportProgressAsync(chunk, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"问题分类执行失败：{ex.Message}");
        }

        var matched = ResolveAnswer(answer, classes);
        if (matched.Fallback)
        {
            await context.ReportProgressAsync($"未能从模型输出解析出分类（输出：{answer}），默认命中第一个分类", cancellationToken);
        }

        // 输入透传 + 命中分类（供下游引用分类 id 与分类名）
        var output = context.Inputs.CloneObject();
        output["result"] = matched.Id;
        output["className"] = matched.Label;
        return NodeExecutionResult.Success(output);
    }

    /// <summary>
    /// 从节点配置解析分类列表（id + 名称），供执行器与验证器共用.
    /// </summary>
    public static List<(string Id, string Label)> ParseClasses(JsonElement config)
    {
        var list = new List<(string, string)>();
        if (config.ValueKind != JsonValueKind.Object || !config.TryGetProperty("classes", out var classesEl) || classesEl.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in classesEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
            var label = item.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String ? labelEl.GetString() : null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                list.Add((id, label ?? string.Empty));
            }
        }

        return list;
    }

    /// <summary>
    /// 解析聊天记录携带条数：非法或缺省用默认值，限制在 [0, <see cref="MaxHistoryCount"/>.
    /// </summary>
    public static int ParseHistoryCount(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object || !config.TryGetProperty("historyCount", out var countEl) || countEl.ValueKind != JsonValueKind.Number)
        {
            return DefaultHistoryCount;
        }

        var value = countEl.GetInt32();
        return Math.Clamp(value, 0, MaxHistoryCount);
    }

    private static string BuildSystemPrompt(List<(string Id, string Label)> classes, string? backgroundKnowledge)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是问题分类器。根据对话历史与当前用户问题，从下面的类型列表中选出最匹配的一个类型。");
        builder.AppendLine("类型列表：");
        for (var i = 0; i < classes.Count; i++)
        {
            builder.AppendLine($"{i + 1}. {classes[i].Label}");
        }

        if (!string.IsNullOrWhiteSpace(backgroundKnowledge))
        {
            builder.AppendLine($"背景知识：{backgroundKnowledge}");
        }

        builder.Append($"只输出类型序号数字（1-{classes.Count}），不要输出其他内容。");
        return builder.ToString();
    }

    /// <summary>
    /// 从模型输出解析命中的分类：优先取回答中的序号数字（1-based），其次按分类名匹配，兜底第一个分类.
    /// </summary>
    private static (string Id, string Label, bool Fallback) ResolveAnswer(string answer, List<(string Id, string Label)> classes)
    {
        var trimmed = (answer ?? string.Empty).Trim();

        var match = Regex.Match(trimmed, @"\d{1,3}");
        if (match.Success && int.TryParse(match.Value, out var number) && number >= 1 && number <= classes.Count)
        {
            var hit = classes[number - 1];
            return (hit.Id, hit.Label, false);
        }

        foreach (var (id, label) in classes)
        {
            if (!string.IsNullOrWhiteSpace(label) && trimmed.Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return (id, label, false);
            }
        }

        var first = classes[0];
        return (first.Id, first.Label, true);
    }
}
