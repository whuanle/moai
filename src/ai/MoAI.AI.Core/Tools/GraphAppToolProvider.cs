using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
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
            Description = "在应用绑定的知识图谱中检索实体及其一跳关系，适合多跳关联问题（如「A 和 B 什么关系」「有哪些 X」）；需要文档原文片段时改用知识库检索。",
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
        var payload = JsonSerializer.Serialize(new
        {
            query,
            count = result.Hits.Count,
            hits = result.Hits.Select(h => new
            {
                graphId = h.KgId,
                nodeId = h.NodeId,
                name = h.Name,
                entityType = h.EntityTypeName,
                description = h.Description,
                score = h.Score,
                neighbors = h.Neighbors.Select(n => new
                {
                    relation = n.RelationName,
                    direction = n.Direction,
                    name = n.Name,
                    description = n.Description,
                }),
            }),
            skipped = result.SkippedHints,
        }, JsonOptions);

        return AppToolResult.Ok(payload);
    }

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
                topK.TryGetInt32(out var value))
            {
                return Math.Clamp(value, MinTopK, MaxTopK);
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
