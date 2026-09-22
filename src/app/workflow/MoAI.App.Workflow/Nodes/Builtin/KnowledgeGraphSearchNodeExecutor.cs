using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 知识图谱检索节点执行器 - 通过 <see cref="IWorkflowGraphSearchClient"/> 在指定知识图谱中做向量检索（topK 实体 + 一跳邻居的子图文本）.
/// 配置：{ "graphId": 1, "topK": 5 }（graphId 为单个知识图谱 id，topK 为召回条数，默认 5、上限 50）.
/// 输入：query（必填，检索查询文本）.
/// 输出：{ query, count, hits: [{kgId, nodeId, name, entityType, description, score}], contents: [string], text }.
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
                // contents 与 hits 同序，每项为该命中含邻居的文本化片段
                contentsArray.Add(hit.Text);
                if (textBuilder.Length > 0)
                {
                    textBuilder.Append("\n\n");
                }

                // Text 已含名称/类型/描述与邻居信息，不再像知识库节点那样追加【标题】前缀
                textBuilder.Append(hit.Text);
            }

            return NodeExecutionResult.Success(new JsonObject
            {
                ["query"] = query,
                ["count"] = hits.Count,
                ["hits"] = hitsArray,
                ["contents"] = contentsArray,
                ["text"] = textBuilder.ToString(),
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
}
