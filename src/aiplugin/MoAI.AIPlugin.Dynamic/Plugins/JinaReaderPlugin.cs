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
/// Jina 网页阅读（动态插件）：使用实例配置中的 API Key 调用 Jina Reader API（r.jina.ai），
/// 抓取目标网页（含需要 JS 渲染的页面）并转换为 LLM 友好的 Markdown 正文，可附带链接/图片摘要.
/// </summary>
[AiPlugin(key: "jina_reader", Name = "Jina 网页阅读", Description = "调用 Jina Reader API 把网页转换为 LLM 友好的 Markdown 正文，支持 JS 渲染页面，可附带链接与图片摘要")]
public class JinaReaderPlugin : IDynamicPluginRuntime<JinaReaderRequest, JinaReaderResponse, JinaReaderConfig>
{
    private const string ApiName = "Jina 网页阅读";

    private static readonly HashSet<string> ReturnFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "markdown",
        "text",
        "html",
        "screenshot",
        "pageshot",
    };

    private readonly IJinaReaderClient _client;
    private JinaReaderConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JinaReaderPlugin"/> class.
    /// </summary>
    /// <param name="client">Jina Reader API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public JinaReaderPlugin(IJinaReaderClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Url": "https://jina.ai/reader",   // 要阅读的网页地址，http/https 开头
              "ReturnFormat": "markdown",        // 正文格式：markdown | text | html | screenshot | pageshot
              "WithLinksSummary": false,         // 是否附带链接摘要
              "WithImagesSummary": false,        // 是否附带图片摘要
              "NoCache": false,                  // 是否绕过缓存强制重新抓取
              "Timeout": null                    // 抓取超时秒数，不填用服务端默认值
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
    public Task<string?> InitAsync(JinaReaderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Task.FromResult<string?>("API Key 不能为空，请先在 https://jina.ai/ 获取");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<JinaReaderResponse> RunAsync(JinaReaderRequest request, CancellationToken cancellationToken)
    {
        var url = (request.Url ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            throw new BusinessException(400, "网页地址 Url 不能为空");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(400, "网页地址 Url 必须是 http/https 开头的完整地址");
        }

        try
        {
            var response = await _client.ReadAsync(
                    JinaAuthorization.Build(_config.ApiKey),
                    "application/json",
                    NormalizeReturnFormat(request.ReturnFormat),
                    request.WithLinksSummary ? "true" : null,
                    request.WithImagesSummary ? "true" : null,
                    request.NoCache ? "true" : null,
                    request.Timeout is > 0 ? request.Timeout.Value.ToString() : null,
                    new JinaReaderQuery { Url = url })
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            // HTTP 200 但响应体 code 非 200 的兜底处理.
            response.EnsureSuccess(ApiName);
            return Map(response);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 密钥无效、429 限流/额度耗尽、451 目标站点拒绝抓取等）。
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

    private static JinaReaderResponse Map(JinaEnvelope<JinaReaderResult> response)
    {
        var data = response.Data ?? new JinaReaderResult();
        return new JinaReaderResponse
        {
            Url = data.Url ?? string.Empty,
            Title = data.Title ?? string.Empty,
            Description = data.Description,
            Content = data.Content ?? string.Empty,
            PublishTime = data.PublishTime,
            Links = data.Links is { Count: > 0 } ? data.Links : null,
            Images = data.Images is { Count: > 0 } ? data.Images : null,
            UsageTokens = data.Usage?.Tokens,
        };
    }
}
