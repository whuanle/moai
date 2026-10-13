using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Jina;
using MoAI.Infra.Jina.Models;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Jina 搜索（动态插件）：使用实例配置中的 API Key 调用 Jina Search API（s.jina.ai），
/// 检索全网并对命中页面做正文提取，返回标题、链接、摘要与 LLM 友好正文.
/// </summary>
[AiPlugin(key: "jina_search", Name = "Jina 搜索", Description = "调用 Jina AI Search API 检索全网，返回每条命中结果的标题、链接、摘要与页面正文（Markdown）")]
public class JinaSearchPlugin : IDynamicPluginRuntime<JinaSearchRequest, JinaSearchResponse, JinaSearchConfig>
{
    private const string ApiName = "Jina 搜索";

    private static readonly HashSet<string> ReturnFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "markdown",
        "text",
        "html",
        "screenshot",
        "pageshot",
    };

    private readonly IJinaSearchClient _client;
    private JinaSearchConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JinaSearchPlugin"/> class.
    /// </summary>
    /// <param name="client">Jina Search API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public JinaSearchPlugin(IJinaSearchClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Query": "latest developments in quantum computing", // 搜索词，可为关键词或自然语言问题
              "ReturnFormat": "markdown",    // 正文格式：markdown | text | html | screenshot | pageshot
              "NoCache": false               // 是否绕过缓存强制重新抓取
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ApiKey": "jina_xxxxxxxxxxxx" // Jina AI API Key，前往 https://jina.ai/ 获取
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(JinaSearchConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Task.FromResult<string?>("API Key 不能为空，请先在 https://jina.ai/ 获取");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<JinaSearchResponse> RunAsync(JinaSearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new BusinessException(400, "搜索词 Query 不能为空");
        }

        var query = request.Query.Trim();
        try
        {
            var response = await _client.SearchAsync(
                    JinaAuthorization.Build(_config.ApiKey),
                    NormalizeReturnFormat(request.ReturnFormat),
                    request.NoCache ? "true" : null,
                    new JinaSearchQuery { Q = query })
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            // HTTP 200 但响应体 code 非 200 的兜底处理.
            response.EnsureSuccess(ApiName);
            return Map(response, query);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 密钥无效、429 限流/额度耗尽等）。
            throw new BusinessException((int)ex.StatusCode, $"{ApiName}调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }
    }

    /// <summary>
    /// 归一化正文格式：空值或非法值回落 markdown，避免无效请求头.
    /// </summary>
    private static string NormalizeReturnFormat(string? returnFormat)
    {
        var format = (returnFormat ?? string.Empty).Trim();
        return ReturnFormats.TryGetValue(format, out var normalized) ? normalized : "markdown";
    }

    private static JinaSearchResponse Map(JinaEnvelope<List<JinaSearchResultItem>> response, string query)
    {
        var results = (response.Data ?? []).Select(x => new JinaSearchPage
        {
            Title = x.Title ?? string.Empty,
            Url = x.Url ?? string.Empty,
            Description = x.Description,
            Content = x.Content,
        }).ToList();

        return new JinaSearchResponse
        {
            Query = query,
            Results = results,
            UsageTokens = response.Data?.Sum(x => x.Usage?.Tokens ?? 0),
        };
    }
}
