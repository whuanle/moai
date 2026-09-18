using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 知识库检索节点执行器 - 通过 <see cref="IWorkflowWikiSearchClient"/> 在指定知识库中做向量检索.
/// 配置：{ "wikiId": 1, "topK": 5 }（wikiId 为单个知识库 id，topK 为召回条数，默认 5、上限 50）.
/// 输入：query（必填，检索查询文本）；wikiId（可选，变量绑定的知识库 id，运行时优先于配置的静态知识库）.
/// 兼容旧版复数形式 wikiIds（输入/配置均可，数组或逗号分隔）.
/// 输出：{ query, count, hits: [{wikiId, documentId, documentName, chunkId, content, score}], contents: [string], text }.
/// </summary>
public class KnowledgeSearchNodeExecutor : INodeExecutor
{
    private const int DefaultTopK = 5;
    private const int MaxTopK = 50;

    private readonly IWorkflowWikiSearchClient _wikiSearchClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeSearchNodeExecutor"/> class.
    /// </summary>
    public KnowledgeSearchNodeExecutor(IWorkflowWikiSearchClient wikiSearchClient)
    {
        _wikiSearchClient = wikiSearchClient;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.KnowledgeSearch;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Inputs.TryGetPropertyValue("query", out var queryNode) || queryNode == null)
        {
            return NodeExecutionResult.Failure("知识库检索节点缺少必需的输入字段：query");
        }

        var query = queryNode is JsonValue ? queryNode.GetValue<string>() : queryNode.ToJsonString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return NodeExecutionResult.Failure("知识库检索节点的查询文本（query）为空");
        }

        // 知识库来源：输入 wikiId（变量绑定，运行时动态解析）优先，其次 config.wikiId（静态选择）
        var wikiIds = GetInputWikiIds(context.Inputs);
        if (wikiIds.Count == 0)
        {
            wikiIds = GetConfigWikiIds(context.Config);
        }

        if (wikiIds.Count == 0)
        {
            return NodeExecutionResult.Failure("知识库检索节点未配置知识库（输入 wikiId 或 config.wikiId）");
        }

        var topK = GetConfigTopK(context.Config);

        try
        {
            var hits = await _wikiSearchClient.SearchAsync(wikiIds, query, topK, cancellationToken);

            var hitsArray = new JsonArray();
            var contentsArray = new JsonArray();
            var textBuilder = new System.Text.StringBuilder();
            foreach (var hit in hits)
            {
                hitsArray.Add(new JsonObject
                {
                    ["wikiId"] = hit.WikiId,
                    ["documentId"] = hit.DocumentId,
                    ["documentName"] = hit.DocumentName,
                    ["chunkId"] = hit.ChunkId,
                    ["content"] = hit.Content,
                    ["score"] = hit.Score.HasValue ? JsonValue.Create(hit.Score.Value) : null,
                });
                contentsArray.Add(hit.Content);
                if (textBuilder.Length > 0)
                {
                    textBuilder.Append("\n\n");
                }

                textBuilder.Append('【').Append(string.IsNullOrWhiteSpace(hit.DocumentName) ? $"知识库 {hit.WikiId}" : hit.DocumentName).Append("】\n").Append(hit.Content);
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
            return NodeExecutionResult.Failure($"知识库检索执行失败：{ex.Message}");
        }
    }

    private static List<long> GetConfigWikiIds(JsonElement config)
    {
        var wikiIds = new List<long>();
        if (config.ValueKind != JsonValueKind.Object)
        {
            return wikiIds;
        }

        // 单数 wikiId（现行为）优先，兼容旧版复数 wikiIds 数组
        if (config.TryGetProperty("wikiId", out var wikiIdEl) && wikiIdEl.ValueKind == JsonValueKind.Number && wikiIdEl.TryGetInt64(out var singleId) && singleId > 0)
        {
            wikiIds.Add(singleId);
            return wikiIds;
        }

        if (config.TryGetProperty("wikiIds", out var wikiIdsEl) && wikiIdsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in wikiIdsEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var id) && id > 0)
                {
                    wikiIds.Add(id);
                }
            }
        }

        return wikiIds;
    }

    /// <summary>
    /// 解析输入绑定的知识库 id：单数 wikiId（数字/数字字符串）优先；兼容旧版复数 wikiIds（数组或逗号分隔字符串）.
    /// </summary>
    private static List<long> GetInputWikiIds(JsonObject inputs)
    {
        var wikiIds = new List<long>();
        if (inputs.TryGetPropertyValue("wikiId", out var singleNode) && singleNode != null)
        {
            AddWikiId(wikiIds, singleNode);
            if (wikiIds.Count > 0)
            {
                return wikiIds;
            }
        }

        if (!inputs.TryGetPropertyValue("wikiIds", out var node) || node == null)
        {
            return wikiIds;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                AddWikiId(wikiIds, item);
            }
        }
        else
        {
            AddWikiId(wikiIds, node);
        }

        return wikiIds;
    }

    private static void AddWikiId(List<long> wikiIds, JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return;
        }

        // 不依赖底层承载类型（JsonElement / CLR 数值、字符串均可）：统一取原始 JSON 文本
        var raw = value.ToJsonString();
        if (raw.Length >= 2 && raw[0] == '"')
        {
            // JSON 字符串：去掉引号后按单个 id 或逗号分隔列表解析
            string? text;
            try
            {
                text = JsonSerializer.Deserialize<string>(raw);
            }
            catch (JsonException)
            {
                return;
            }

            AddIdsFromText(wikiIds, text);
        }
        else if (long.TryParse(raw, out var id) && id > 0)
        {
            wikiIds.Add(id);
        }
    }

    private static void AddIdsFromText(List<long> wikiIds, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, out var id) && id > 0)
            {
                wikiIds.Add(id);
            }
        }
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
