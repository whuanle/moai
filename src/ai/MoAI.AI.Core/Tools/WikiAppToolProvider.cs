using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Wiki.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 知识库工具来源：把应用绑定的知识库封装为一个「检索」工具.
/// </summary>
[InjectOnScoped]
public sealed class WikiAppToolProvider : IAppToolProvider
{
    /// <summary>
    /// 知识库检索工具名称.
    /// </summary>
    public const string ToolName = "search_knowledge_base";

    private const int TopPerWiki = 5;

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
        var tool = new AppTool
        {
            Name = ToolName,
            Title = "知识库检索",
            Description = "在应用绑定的知识库中检索与问题相关的资料片段，返回命中的文档名与内容。",
            Kind = "wiki",
            ParametersExample = "{\"query\":\"要检索的关键词或问题\"}",
            InvokeAsync = (argsJson, ct) => SearchAsync(wikiIds, argsJson, ct),
        };

        return Task.FromResult<IReadOnlyList<AppTool>>([tool]);
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
            document = string.IsNullOrWhiteSpace(hit.DocumentName) ? $"知识库 {hit.WikiId}" : hit.DocumentName,
            content = hit.Content,
        }).ToList();

        return AppToolResult.Ok(JsonSerializer.Serialize(new { query, count = payload.Count, hits = payload }, JsonOptions));
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
