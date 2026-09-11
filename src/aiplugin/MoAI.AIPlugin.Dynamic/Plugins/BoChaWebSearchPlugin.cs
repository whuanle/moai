using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.BoCha;
using MoAI.Infra.BoCha.Models;
using MoAI.Infra.Exceptions;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 博查全网搜索（动态插件）：使用实例配置中的 API Key 调用博查 Web Search API，检索全网网页与图片.
/// </summary>
[AiPlugin(key: "bocha_web_search", Name = "博查全网搜索", Description = "调用博查 Web Search API 检索全网网页和图片，返回标题、链接、摘要、站点、发布时间等")]
public class BoChaWebSearchPlugin : IDynamicPluginRuntime<BoChaWebSearchRequest, BoChaWebSearchResponse, BoChaWebSearchConfig>
{
    private const int MinCount = 1;
    private const int MaxCount = 50;
    private const string NoLimit = "noLimit";

    private readonly IBoChaClient _client;
    private BoChaWebSearchConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BoChaWebSearchPlugin"/> class.
    /// </summary>
    /// <param name="client">博查 API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public BoChaWebSearchPlugin(IBoChaClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Query": "阿里巴巴2024年的ESG报告", // 搜索词
              "Freshness": "noLimit",             // noLimit | oneDay | oneWeek | oneMonth | oneYear | 2025-01-01..2025-04-06
              "Summary": true,                    // 是否返回网页文本摘要
              "Count": 10,                        // 返回条数 1-50
              "Include": null,                    // 仅在这些站点内搜索，如 qq.com|m.163.com
              "Exclude": null                     // 排除这些站点
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ApiKey": "sk-xxxxxxxx" // 博查开放平台 API Key，前往 https://open.bocha.cn 的「API KEY 管理」获取
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(BoChaWebSearchConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Task.FromResult<string?>("API Key 不能为空，请先在 https://open.bocha.cn 获取");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<BoChaWebSearchResponse> RunAsync(BoChaWebSearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new BusinessException(400, "搜索词 Query 不能为空");
        }

        var query = request.Query.Trim();
        var searchRequest = new WebSearchRequest
        {
            Query = query,
            Freshness = string.IsNullOrWhiteSpace(request.Freshness) ? NoLimit : request.Freshness.Trim(),
            Summary = request.Summary,
            Count = Math.Clamp(request.Count, MinCount, MaxCount),
            Include = request.Include,
            Exclude = request.Exclude,
        };

        WebSearchResponse response;
        try
        {
            response = await _client.WebSearchAsync(BoChaAuthorization.Build(_config.ApiKey), searchRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 密钥无效、403 余额不足、429 限流等）。
            throw new BusinessException((int)ex.StatusCode, $"博查 Web Search 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        // HTTP 200 但响应体 code 非 200 的兜底处理.
        _client.HandleApiError(response);
        return Map(response.Data, query);
    }

    private static BoChaWebSearchResponse Map(WebSearchData? data, string query)
    {
        if (data == null)
        {
            return new BoChaWebSearchResponse { Query = query };
        }

        return new BoChaWebSearchResponse
        {
            Query = string.IsNullOrWhiteSpace(data.QueryContext?.OriginalQuery) ? query : data.QueryContext.OriginalQuery,
            TotalEstimatedMatches = data.WebPages?.TotalEstimatedMatches ?? 0,
            SomeResultsRemoved = data.WebPages?.SomeResultsRemoved ?? false,
            WebPages = data.WebPages?.Value?.Select(x => new BoChaWebPage
            {
                Name = x.Name ?? string.Empty,
                Url = x.Url ?? string.Empty,
                Snippet = x.Snippet ?? string.Empty,
                Summary = x.Summary,
                SiteName = x.SiteName,
                SiteIcon = x.SiteIcon,
                DatePublished = x.DatePublished,
            }).ToList() ?? [],
            Images = data.Images?.Value?.Select(x => new BoChaWebImage
            {
                Name = x.Name,
                ThumbnailUrl = x.ThumbnailUrl,
                ContentUrl = x.ContentUrl,
                HostPageUrl = x.HostPageUrl,
                Width = x.Width,
                Height = x.Height,
            }).ToList() ?? [],
        };
    }
}
