using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Metaso;
using MoAI.Infra.Metaso.Models;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 秘塔搜索（动态插件）：使用实例配置中的 API Key 调用秘塔搜索 API（metaso.cn），
/// 按范围检索网页/文库/论文/图片/视频/播客，返回标题、链接、摘要等（开启摘要召回时附带综合摘要）.
/// </summary>
[AiPlugin(key: "metaso_search", Name = "秘塔搜索", Description = "调用秘塔 AI 搜索 API 检索网页/文库/论文/图片/视频/播客，返回标题、链接、摘要，可附来源网页原文与综合摘要")]
public class MetasoSearchPlugin : IDynamicPluginRuntime<MetasoSearchRequest, MetasoSearchResponse, MetasoSearchConfig>
{
    private const string ApiName = "秘塔搜索";
    private const int MinSize = 1;
    private const int MaxSize = 100;

    private static readonly HashSet<string> Scopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "webpage",
        "document",
        "paper",
        "image",
        "video",
        "podcast",
    };

    private readonly IMetasoClient _client;
    private MetasoSearchConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MetasoSearchPlugin"/> class.
    /// </summary>
    /// <param name="client">秘塔 API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public MetasoSearchPlugin(IMetasoClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Query": "量子计算最新进展",   // 搜索词
              "Scope": "webpage",           // webpage | document | paper | image | video | podcast
              "Size": 10,                   // 返回条数 1-100
              "IncludeSummary": false,      // 摘要召回增强，开启后返回综合摘要 Summary
              "IncludeRawContent": false,   // 抓取来源网页原文（RawContent）
              "ConciseSnippet": false       // 返回精简的原文匹配信息
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ApiKey": "mk-xxxxxxxx" // 秘塔 API Key，前往 https://metaso.cn/search-api/api-keys 获取
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(MetasoSearchConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Task.FromResult<string?>("API Key 不能为空，请先在 https://metaso.cn/search-api/api-keys 获取");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<MetasoSearchResponse> RunAsync(MetasoSearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new BusinessException(400, "搜索词 Query 不能为空");
        }

        var scope = (request.Scope ?? string.Empty).Trim();
        if (scope.Length == 0)
        {
            scope = "webpage";
        }
        else if (!Scopes.TryGetValue(scope.ToLowerInvariant(), out var normalized))
        {
            throw new BusinessException(400, $"搜索范围 Scope 仅允许 webpage/document/paper/image/video/podcast，当前值：{scope}");
        }
        else
        {
            scope = normalized;
        }

        var query = request.Query.Trim();
        var searchRequest = new MetasoSearchQuery
        {
            Q = query,
            Scope = scope,
            IncludeSummary = request.IncludeSummary,
            IncludeRawContent = request.IncludeRawContent,
            Size = Math.Clamp(request.Size, MinSize, MaxSize),
            ConciseSnippet = request.ConciseSnippet,
        };

        MetasoSearchPayload response;
        try
        {
            response = await _client.SearchAsync(MetasoAuthorization.Build(_config.ApiKey), searchRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 密钥无效、402 点数不足、429 限流等）。
            throw new BusinessException((int)ex.StatusCode, $"{ApiName}调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        return Map(response, query, scope);
    }

    private static MetasoSearchResponse Map(MetasoSearchPayload? data, string query, string scope)
    {
        if (data == null)
        {
            return new MetasoSearchResponse { Query = query, Scope = scope };
        }

        // 网页/文库/视频结果字段同构，统一进 Results，便于 AI 与流程编排按一个列表消费.
        var results = new List<MetasoSearchPage>();
        foreach (var page in data.Webpages ?? Enumerable.Empty<MetasoWebpageResult>())
        {
            results.Add(new MetasoSearchPage
            {
                Title = page.Title ?? string.Empty,
                Link = page.Link ?? string.Empty,
                Snippet = page.Snippet,
                Date = page.Date,
                Score = page.Score,
                Position = page.Position,
                RawContent = page.RawContent,
            });
        }

        foreach (var item in (data.Documents ?? Enumerable.Empty<MetasoDocumentResult>())
            .Concat(data.Videos ?? Enumerable.Empty<MetasoDocumentResult>()))
        {
            results.Add(new MetasoSearchPage
            {
                Title = item.Title ?? string.Empty,
                Link = item.Link ?? string.Empty,
                Snippet = item.Snippet,
                Position = item.Position,
            });
        }

        return new MetasoSearchResponse
        {
            Query = query,
            Scope = scope,
            Results = results,
            Images = (data.Images ?? []).Select(x => new MetasoSearchImage
            {
                Title = x.Title,
                Link = x.Link ?? string.Empty,
                Thumbnail = x.Thumbnail,
            }).ToList(),
            Total = data.Total,
            Summary = data.Summary,
            Credits = data.Credits,
        };
    }
}
