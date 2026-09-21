using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// AI 抽取出的实体草案.
/// </summary>
/// <param name="Name">实体名称.</param>
/// <param name="EntityType">实体类型名称.</param>
/// <param name="Description">描述.</param>
/// <param name="Properties">属性值（属性名→值，均为字符串）.</param>
public sealed record KnowledgeGraphImportNodeDraft(
    string Name,
    string EntityType,
    string Description,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// AI 抽取出的关系草案.
/// </summary>
/// <param name="Source">起点实体名称.</param>
/// <param name="Target">终点实体名称.</param>
/// <param name="RelationType">关系类型名称.</param>
public sealed record KnowledgeGraphImportEdgeDraft(string Source, string Target, string RelationType);

/// <summary>
/// 解析 AI 输出中的图谱抽取 JSON（容错 markdown code fence 与 camelCase/snake_case 字段名）.
/// </summary>
public static class KnowledgeGraphImportParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 从模型文本中解析实体与关系草案；无可解析内容返回空列表.
    /// </summary>
    /// <param name="aiOutput">模型原始输出.</param>
    /// <returns>实体与关系草案.</returns>
    public static (IReadOnlyList<KnowledgeGraphImportNodeDraft> Nodes, IReadOnlyList<KnowledgeGraphImportEdgeDraft> Edges) Parse(string? aiOutput)
    {
        var nodes = new List<KnowledgeGraphImportNodeDraft>();
        var edges = new List<KnowledgeGraphImportEdgeDraft>();
        var json = ExtractJson(aiOutput);
        if (string.IsNullOrWhiteSpace(json))
        {
            return (nodes, edges);
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return (nodes, edges);
        }

        if (root == null)
        {
            return (nodes, edges);
        }

        if (root["nodes"] is JsonArray nodesArray)
        {
            foreach (var item in nodesArray.OfType<JsonObject>())
            {
                var name = PickString(item, "name", "entityName", "entity_name");
                var entityType = PickString(item, "entityType", "entity_type", "type", "label");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(entityType))
                {
                    continue;
                }

                var properties = new Dictionary<string, string>(StringComparer.Ordinal);
                if (item["properties"] is JsonObject propsObject)
                {
                    foreach (var (key, value) in propsObject)
                    {
                        var text = value switch
                        {
                            JsonValue v when v.TryGetValue<string>(out var s) => s,
                            JsonValue v => v.ToJsonString(Options).Trim('"'),
                            _ => string.Empty,
                        };
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            properties[key] = text;
                        }
                    }
                }

                nodes.Add(new KnowledgeGraphImportNodeDraft(name.Trim(), entityType.Trim(), PickString(item, "description") ?? string.Empty, properties));
            }
        }

        if (root["edges"] is JsonArray edgesArray)
        {
            foreach (var item in edgesArray.OfType<JsonObject>())
            {
                var source = PickString(item, "source", "sourceName", "source_name", "from");
                var target = PickString(item, "target", "targetName", "target_name", "to");
                var relationType = PickString(item, "relationType", "relation_type", "relation", "type", "label");
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(relationType))
                {
                    continue;
                }

                edges.Add(new KnowledgeGraphImportEdgeDraft(source.Trim(), target.Trim(), relationType.Trim()));
            }
        }

        return (nodes, edges);
    }

    private static string? PickString(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (var (actualKey, value) in obj)
            {
                if (string.Equals(actualKey, key, StringComparison.OrdinalIgnoreCase) && value is JsonValue v && v.TryGetValue<string>(out var s))
                {
                    return s;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 提取输出中的 JSON 片段：优先 ```json 代码块，其次首个 { 到最后一个 }.
    /// </summary>
    private static string? ExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var fenced = text.IndexOf("```", StringComparison.Ordinal);
        if (fenced >= 0)
        {
            var start = text.IndexOf('{', fenced);
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text[start..(end + 1)];
            }
        }

        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        if (first >= 0 && last > first)
        {
            return text[first..(last + 1)];
        }

        return null;
    }
}
