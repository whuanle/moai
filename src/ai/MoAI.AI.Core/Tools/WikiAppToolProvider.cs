using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 知识库工具来源：把应用绑定的知识库封装为「检索」与「按序号取片段」两个工具.
/// </summary>
[InjectOnScoped]
public sealed class WikiAppToolProvider : IAppToolProvider
{
    /// <summary>
    /// 知识库检索工具名称.
    /// </summary>
    public const string ToolName = "search_knowledge_base";

    /// <summary>
    /// 知识库片段获取工具名称.
    /// </summary>
    public const string ChunkToolName = "get_knowledge_base_chunk";

    private const int TopPerWiki = 5;
    private const int MaxChunkIndexes = 10;

    private readonly IWikiSearchService _wikiSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiAppToolProvider"/> class.
    /// </summary>
    /// <param name="wikiSearchService">知识库检索服务.</param>
    public WikiAppToolProvider(IWikiSearchService wikiSearchService)
    {
        _wikiSearchService = wikiSearchService;
    }

    /// <inheritdoc/>
    public int Order => 20;

    /// <inheritdoc/>
    public Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        if (context.WikiIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<AppTool>>([]);
        }

        var wikiIds = context.WikiIds;
        var searchTool = new AppTool
        {
            Name = ToolName,
            Title = "知识库检索",
            Description = "在应用绑定的知识库中检索与问题相关的资料片段，返回命中的文档、片段在文档中的序号（chunkIndex，从 0 开始）、文档切片总数（totalChunks）、得分与相邻上下文片段（context）。如某文档的片段信息不足以回答，可再用 get_knowledge_base_chunk 按文档 id 与片段序号获取该文档的其它片段。",
            Kind = "wiki",
            ParametersExample = "{\"query\":\"要检索的关键词或问题\"}",
            InvokeAsync = (argsJson, ct) => SearchAsync(wikiIds, argsJson, ct),
        };

        var chunkTool = new AppTool
        {
            Name = ChunkToolName,
            Title = "知识库片段获取",
            Description = "按文档 id 与片段序号（chunkIndex，从 0 开始）获取知识库文档的原文片段，一次最多 10 个序号。用于检索结果上下文不足时继续读取同一文档的其它片段（例如命中片段的前一段或后几段）。documentId 与可用序号范围来自 search_knowledge_base 的返回。",
            Kind = "wiki",
            ParametersExample = "{\"documentId\":123,\"chunkIndexes\":[3,4]}",
            InvokeAsync = (argsJson, ct) => GetChunksAsync(wikiIds, argsJson, ct),
        };

        return Task.FromResult<IReadOnlyList<AppTool>>([searchTool, chunkTool]);
    }

    private async Task<AppToolResult> SearchAsync(IReadOnlyList<long> wikiIds, string? argsJson, CancellationToken cancellationToken)
    {
        var query = ExtractQuery(argsJson);
        if (string.IsNullOrWhiteSpace(query))
        {
            return AppToolResult.Fail("缺少参数 query.");
        }

        var hits = await _wikiSearchService.SearchAsync(wikiIds, query, TopPerWiki, cancellationToken).ConfigureAwait(false);
        var payload = hits.Select(hit => new
        {
            wikiId = hit.WikiId,
            documentId = hit.DocumentId,
            document = string.IsNullOrWhiteSpace(hit.DocumentName) ? $"知识库 {hit.WikiId}" : hit.DocumentName,
            chunkId = hit.ChunkId.ToString(),
            chunkIndex = hit.ChunkIndex,
            totalChunks = hit.DocumentChunkCount,
            score = hit.Score,
            rerankScore = hit.RerankScore,
            content = hit.Content,
            context = hit.Context.Select(x => new { chunkIndex = x.ChunkIndex, content = x.Content }).ToList(),
        }).ToList();

        return AppToolResult.Ok(JsonSerializer.Serialize(new { query, count = payload.Count, hits = payload }, JsonOptions));
    }

    private async Task<AppToolResult> GetChunksAsync(IReadOnlyList<long> wikiIds, string? argsJson, CancellationToken cancellationToken)
    {
        if (!TryParseChunkArgs(argsJson, out var documentId, out var chunkIndexes))
        {
            return AppToolResult.Fail($"缺少参数 documentId 或 chunkIndexes（序号数组，1-{MaxChunkIndexes} 个非负整数）.");
        }

        try
        {
            var result = await _wikiSearchService.GetDocumentChunksAsync(wikiIds, documentId, chunkIndexes, cancellationToken).ConfigureAwait(false);
            var payload = new
            {
                documentId = result.DocumentId,
                document = result.DocumentName,
                count = result.Chunks.Count,
                chunks = result.Chunks.Select(x => new { chunkIndex = x.ChunkIndex, chunkId = x.ChunkId.ToString(), content = x.Content }).ToList(),
            };

            return AppToolResult.Ok(JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (BusinessException ex)
        {
            return AppToolResult.Fail(ex.Message);
        }
    }

    private static bool TryParseChunkArgs(string? argsJson, out int documentId, out List<int> chunkIndexes)
    {
        documentId = 0;
        chunkIndexes = [];
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("documentId", out var documentIdEl))
            {
                return false;
            }

            if (documentIdEl.ValueKind == JsonValueKind.Number && documentIdEl.TryGetInt32(out var id))
            {
                documentId = id;
            }
            else if (documentIdEl.ValueKind == JsonValueKind.String && int.TryParse(documentIdEl.GetString(), out var parsedId))
            {
                documentId = parsedId;
            }
            else
            {
                return false;
            }

            if (documentId <= 0)
            {
                return false;
            }

            if (!root.TryGetProperty("chunkIndexes", out var indexesEl) || indexesEl.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in indexesEl.EnumerateArray())
            {
                int index;
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number))
                {
                    index = number;
                }
                else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var parsed))
                {
                    index = parsed;
                }
                else
                {
                    return false;
                }

                if (index < 0)
                {
                    return false;
                }

                chunkIndexes.Add(index);
            }

            return chunkIndexes.Count > 0 && chunkIndexes.Count <= MaxChunkIndexes;
        }
        catch (JsonException)
        {
            return false;
        }
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
