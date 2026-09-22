using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 知识图谱检索节点执行器 - 通过 <see cref="IWorkflowGraphSearchClient"/> 在指定知识图谱中做向量检索（topK 实体 + 一跳邻居的子图文本）.
/// 配置：{ "graphId": 1, "topK": 5 }（graphId 为单个知识图谱 id，topK 为召回条数，默认 5、上限 50）.
/// 输入：query（必填，检索查询文本）.
/// 输出：{ query, count, hits: [{kgId, nodeId, name, entityType, description, score}], contents: [string], text }；
/// contents 与 text 均由含名称/类型/描述/邻居的文本化片段构成（与检索 API 的 Text 同源同形），text 超长时截断.
/// </summary>
/// <remarks>
/// 与知识库检索节点（knowledgeSearch）的差异：v1 仅支持 config.graphId 静态选择图谱，
/// 不提供 graphId 输入变量绑定（图谱不随运行时变化，静态配置已满足选图诉求，且保存/发布/调试入口均有团队归属守卫）.
/// hits 元素保持扁平（不含 neighbors 字段），邻居信息在 text/contents 的文本化片段中.
/// </remarks>
public class KnowledgeGraphSearchNodeExecutor : INodeExecutor
{
    private const int DefaultTopK = 5;
    private const int MaxTopK = 50;

    // 与检索 API 侧 GraphSearchTextHelper（KG.Shared）的上限/截断标记保持一致；
    // 引擎工程不引用 KG.Shared（端口解耦），故在此本地收口
    private const int MaxTextLength = 8192;

    private readonly IWorkflowGraphSearchClient _graphSearchClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphSearchNodeExecutor"/> class.
    /// </summary>
    public KnowledgeGraphSearchNodeExecutor(IWorkflowGraphSearchClient graphSearchClient)
    {
        _graphSearchClient = graphSearchClient;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.KnowledgeGraphSearch;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Inputs.TryGetPropertyValue("query", out var queryNode) || queryNode == null)
        {
            return NodeExecutionResult.Failure("知识图谱检索节点缺少必需的输入字段：query");
        }

        var query = queryNode is JsonValue ? queryNode.GetValue<string>() : queryNode.ToJsonString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return NodeExecutionResult.Failure("知识图谱检索节点的查询文本（query）为空");
        }

        var graphId = GetConfigGraphId(context.Config);
        if (graphId == null)
        {
            return NodeExecutionResult.Failure("知识图谱检索节点未配置知识图谱（config.graphId）");
        }

        var topK = GetConfigTopK(context.Config);

        try
        {
            var hits = await _graphSearchClient.SearchAsync([graphId.Value], query, topK, cancellationToken);

            var hitsArray = new JsonArray();
            var contentsArray = new JsonArray();
            var textBuilder = new System.Text.StringBuilder();
            foreach (var hit in hits)
            {
                hitsArray.Add(new JsonObject
                {
                    ["kgId"] = hit.KgId,
                    ["nodeId"] = hit.NodeId,
                    ["name"] = hit.Name,
                    ["entityType"] = hit.EntityTypeName,
                    ["description"] = hit.Description,
                    ["score"] = hit.Score.HasValue ? JsonValue.Create(hit.Score.Value) : null,
                });
                // contents 与 hits 同序，每项为该命中的完整片段（头部【名称（类型）】描述 + 邻居行，与 text 段同源同形）
                contentsArray.Add(hit.Text);
                if (textBuilder.Length > 0)
                {
                    textBuilder.Append("\n\n");
                }

                textBuilder.Append(hit.Text);
            }

            return NodeExecutionResult.Success(new JsonObject
            {
                ["query"] = query,
                ["count"] = hits.Count,
                ["hits"] = hitsArray,
                ["contents"] = contentsArray,
                ["text"] = TruncateText(textBuilder.ToString()),
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"知识图谱检索执行失败：{ex.Message}");
        }
    }

    private static long? GetConfigGraphId(JsonElement config)
    {
        if (config.ValueKind == JsonValueKind.Object
            && config.TryGetProperty("graphId", out var graphIdEl)
            && graphIdEl.ValueKind == JsonValueKind.Number
            && graphIdEl.TryGetInt64(out var graphId)
            && graphId > 0)
        {
            return graphId;
        }

        return null;
    }

    private static int GetConfigTopK(JsonElement config)
    {
        if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty("topK", out var topKEl) && topKEl.ValueKind == JsonValueKind.Number && topKEl.TryGetInt32(out var topK))
        {
            return Math.Clamp(topK, 1, MaxTopK);
        }

        return DefaultTopK;
    }

    /// <summary>
    /// 超长截断（与检索 API 的 GraphSearchTextHelper.Truncate 行为一致）：超限截取前缀并追加「…(已截断)」.
    /// </summary>
    private static string TruncateText(string text)
        => text.Length <= MaxTextLength ? text : text[..MaxTextLength] + "…(已截断)";
}
