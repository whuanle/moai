using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.KnowledgeGraph.External;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 结构化导入 JSON 解析器：容错解析导入页提交的 JSON 文本（AllowTrailingCommas + 注释跳过），
/// 字段缺失/类型错误给出带行定位的中文错误；解析结果交 <see cref="KnowledgeGraphDataImportService"/> 落库.
/// </summary>
public static class KnowledgeGraphJsonImportParser
{
    /// <summary>
    /// 解析导入 JSON 文本为载荷；结构非法抛 400（消息含定位信息）.
    /// </summary>
    /// <param name="content">JSON 文本.</param>
    /// <returns>返回 <see cref="KnowledgeGraphImportPayload"/>.</returns>
    public static KnowledgeGraphImportPayload Parse(string content)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(content, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
        }
        catch (JsonException ex)
        {
            throw new Infra.Exceptions.BusinessException($"JSON 解析失败（第 {ex.LineNumber + 1} 行）：{ex.Message}.") { StatusCode = 400 };
        }

        if (root == null)
        {
            throw new Infra.Exceptions.BusinessException("导入内容必须是 JSON 对象，包含 nodes/edges 数组.") { StatusCode = 400 };
        }

        var payload = new KnowledgeGraphImportPayload
        {
            Mode = ReadString(root, "mode") ?? ImportExternalGraphDataCommand.ModeUpsert,
            AutoCreateTypes = ReadBool(root, "autoCreateTypes") ?? false,
            ValidateOnly = ReadBool(root, "validateOnly") ?? false,
            DetectDuplicates = ReadBool(root, "detectDuplicates") ?? true,
        };

        if (root["nodes"] != null)
        {
            if (root["nodes"] is not JsonArray nodesArray)
            {
                throw new Infra.Exceptions.BusinessException("nodes 必须是数组.") { StatusCode = 400 };
            }

            var nodes = new List<ExternalImportNodeItem>();
            for (var i = 0; i < nodesArray.Count; i++)
            {
                if (nodesArray[i] is not JsonObject item)
                {
                    throw new Infra.Exceptions.BusinessException($"nodes[{i}] 必须是对象.") { StatusCode = 400 };
                }

                var name = ReadString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new Infra.Exceptions.BusinessException($"nodes[{i}].name 不能为空.") { StatusCode = 400 };
                }

                nodes.Add(new ExternalImportNodeItem
                {
                    Key = ReadString(item, "key"),
                    EntityTypeId = ReadLong(item, "entityTypeId"),
                    EntityTypeName = ReadString(item, "entityTypeName"),
                    Name = name.Trim(),
                    Description = ReadString(item, "description"),
                    Properties = ReadStringMap(item, "properties", $"nodes[{i}].properties"),
                });
            }

            payload = new KnowledgeGraphImportPayload
            {
                Mode = payload.Mode,
                AutoCreateTypes = payload.AutoCreateTypes,
                ValidateOnly = payload.ValidateOnly,
                Nodes = nodes,
                Edges = payload.Edges,
            };
        }

        if (root["edges"] != null)
        {
            if (root["edges"] is not JsonArray edgesArray)
            {
                throw new Infra.Exceptions.BusinessException("edges 必须是数组.") { StatusCode = 400 };
            }

            var edges = new List<ExternalImportEdgeItem>();
            for (var i = 0; i < edgesArray.Count; i++)
            {
                if (edgesArray[i] is not JsonObject item)
                {
                    throw new Infra.Exceptions.BusinessException($"edges[{i}] 必须是对象.") { StatusCode = 400 };
                }

                var source = ReadNodeRef(item, "source", i, "source");
                var target = ReadNodeRef(item, "target", i, "target");
                if (source == null || target == null)
                {
                    throw new Infra.Exceptions.BusinessException($"edges[{i}].source/target 必须是对象，且提供 nodeId、key、name 之一.") { StatusCode = 400 };
                }

                edges.Add(new ExternalImportEdgeItem
                {
                    RelationTypeId = ReadLong(item, "relationTypeId"),
                    RelationTypeName = ReadString(item, "relationTypeName"),
                    Source = source,
                    Target = target,
                });
            }

            payload = new KnowledgeGraphImportPayload
            {
                Mode = payload.Mode,
                AutoCreateTypes = payload.AutoCreateTypes,
                ValidateOnly = payload.ValidateOnly,
                Nodes = payload.Nodes,
                Edges = edges,
            };
        }

        return payload;
    }

    private static ExternalImportNodeRef? ReadNodeRef(JsonObject parent, string field, int index, string label)
    {
        if (parent[field] is not JsonObject refObject)
        {
            return null;
        }

        var nodeRef = new ExternalImportNodeRef
        {
            NodeId = ReadString(refObject, "nodeId"),
            Key = ReadString(refObject, "key"),
            Name = ReadString(refObject, "name"),
            EntityTypeName = ReadString(refObject, "entityTypeName"),
        };
        if (!ExternalImportNodeRef.HasAnyRef(nodeRef))
        {
            throw new Infra.Exceptions.BusinessException($"edges[{index}].{label} 必须提供 nodeId、key、name 之一.") { StatusCode = 400 };
        }

        return nodeRef;
    }

    private static string? ReadString(JsonObject obj, string field)
    {
        if (obj[field] is not JsonValue v)
        {
            return null;
        }

        return v.TryGetValue<string>(out var s) ? s : v.ToJsonString(System.Text.Json.JsonSerializerOptions.Default).Trim('"');
    }

    private static long? ReadLong(JsonObject obj, string field)
    {
        if (obj[field] is JsonValue v && v.TryGetValue<long>(out var value))
        {
            return value;
        }

        if (obj[field] is JsonValue s && s.TryGetValue<string>(out var text) && long.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? ReadBool(JsonObject obj, string field)
    {
        if (obj[field] is JsonValue v && v.TryGetValue<bool>(out var value))
        {
            return value;
        }

        return null;
    }

    private static Dictionary<string, string>? ReadStringMap(JsonObject obj, string field, string label)
    {
        if (obj[field] is not JsonObject map)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in map)
        {
            var text = value switch
            {
                JsonValue v when v.TryGetValue<string>(out var s) => s,
                JsonValue v => v.ToJsonString(System.Text.Json.JsonSerializerOptions.Default).Trim('"'),
                _ => string.Empty,
            };
            if (!string.IsNullOrWhiteSpace(text))
            {
                result[key] = text;
            }
        }

        return result;
    }
}
