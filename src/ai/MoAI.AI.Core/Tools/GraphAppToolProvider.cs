using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 知识图谱工具来源：把应用绑定的知识图谱封装为一个「检索」工具.
/// </summary>
[InjectOnScoped]
public sealed class GraphAppToolProvider : IAppToolProvider
{
    /// <summary>
    /// 知识图谱检索工具名称.
    /// </summary>
    public const string ToolName = "search_knowledge_graph";

    private const int DefaultTopK = 5;

    private const int MinTopK = 1;

    private const int MaxTopK = 20;

    private const int MaxPayloadLength = 16 * 1024;

    private const int HitDescriptionMaxLength = 300;

    private const int NeighborDescriptionMaxLength = 150;

    private const int NameMaxLength = 60;

    private const int RelationNameMaxLength = 40;

    private readonly IGraphSearchService _graphSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphAppToolProvider"/> class.
    /// </summary>
    /// <param name="graphSearchService">知识图谱检索服务.</param>
    public GraphAppToolProvider(IGraphSearchService graphSearchService)
    {
        _graphSearchService = graphSearchService;
    }

    /// <inheritdoc/>
    public int Order => 21;

    /// <inheritdoc/>
    public Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        if (context.GraphIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<AppTool>>([]);
        }

        var graphIds = context.GraphIds;
        var tool = new AppTool
        {
            Name = ToolName,
            Title = "知识图谱检索",
            Description = "在应用绑定的知识图谱中检索实体及其直接关系，适合「A 和 B 什么关系」「与 X 相关联的有哪些实体」类问题；仅返回一跳关系，跨多步的链式问题建议拆步提问；需要文档原文片段时改用知识库检索。topK 可选（1-20，默认 5，为每张绑定图谱各自的召回数）。",
            Kind = "graph",
            ParametersExample = "{\"query\":\"要检索的关键词或问题\",\"topK\":5}",
            InvokeAsync = (argsJson, ct) => SearchAsync(graphIds, argsJson, ct),
        };

        return Task.FromResult<IReadOnlyList<AppTool>>([tool]);
    }

    private async Task<AppToolResult> SearchAsync(IReadOnlyList<long> graphIds, string? argsJson, CancellationToken cancellationToken)
    {
        var query = ExtractQuery(argsJson);
        if (string.IsNullOrWhiteSpace(query))
        {
            return AppToolResult.Fail("缺少参数 query.");
        }

        var topK = ExtractTopK(argsJson);
        var result = await _graphSearchService.SearchAsync(graphIds, query, topK, cancellationToken: cancellationToken).ConfigureAwait(false);
        var payload = BuildPayload(query, result);

        return AppToolResult.Ok(payload);
    }

    private static string BuildPayload(string query, GraphSearchResult result)
    {
        var hits = new JsonArray();
        var root = new JsonObject
        {
            ["query"] = query,
            ["count"] = 0,
            ["hits"] = hits,
        };

        var truncated = false;
        foreach (var hit in result.Hits)
        {
            var hitNode = BuildHit(hit);
            hits.Add(hitNode);
            if (root.ToJsonString(JsonOptions).Length > MaxPayloadLength)
            {
                if (hits.Count > 1)
                {
                    hits.Remove(hitNode);
                }

                truncated = true;
                break;
            }
        }

        root["count"] = hits.Count;
        if (truncated)
        {
            root["hitsTruncated"] = true;
        }

        var skipped = new JsonArray();
        foreach (var hint in result.SkippedHints)
        {
            skipped.Add(hint);
        }

        root["skipped"] = skipped;
        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject BuildHit(GraphSearchHit hit)
    {
        var neighbors = new JsonArray();
        foreach (var neighbor in hit.Neighbors)
        {
            neighbors.Add(new JsonObject
            {
                ["relation"] = Truncate(neighbor.RelationName, RelationNameMaxLength),
                ["direction"] = neighbor.Direction,
                ["name"] = Truncate(neighbor.Name, NameMaxLength),
                ["description"] = Truncate(neighbor.Description, NeighborDescriptionMaxLength),
            });
        }

        return new JsonObject
        {
            ["graphId"] = hit.KgId,
            ["nodeId"] = hit.NodeId,
            ["name"] = Truncate(hit.Name, NameMaxLength),
            ["entityType"] = Truncate(hit.EntityTypeName, NameMaxLength),
            ["description"] = Truncate(hit.Description, HitDescriptionMaxLength),
            ["score"] = hit.Score,
            ["neighbors"] = neighbors,
        };
    }

    private static string Truncate(string? value, int maxLength) => string.IsNullOrEmpty(value) || value.Length <= maxLength ? value ?? string.Empty : value[..maxLength] + "…";

    private static string? ExtractQuery(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson.Trim() == "{}")
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("query", out var query) &&
                query.ValueKind == JsonValueKind.String)
            {
                return query.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ExtractTopK(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson.Trim() == "{}")
        {
            return DefaultTopK;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("topK", out var topK) &&
                topK.ValueKind == JsonValueKind.Number &&
                topK.TryGetDouble(out var value))
            {
                return (int)Math.Clamp(value, MinTopK, MaxTopK);
            }

            return DefaultTopK;
        }
        catch (JsonException)
        {
            return DefaultTopK;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
