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
/// 秘塔网页阅读（动态插件）：使用实例配置中的 API Key 调用秘塔阅读 API（metaso.cn），
/// 抓取目标网页并转换为 LLM 友好的 Markdown 正文，json 格式可附带标题/作者/日期等结构化字段.
/// </summary>
[AiPlugin(key: "metaso_reader", Name = "秘塔网页阅读", Description = "调用秘塔 Reader API 把网页转换为 LLM 友好的 Markdown 正文，json 格式可附带标题/作者/日期结构化字段")]
public class MetasoReaderPlugin : IDynamicPluginRuntime<MetasoReaderRequest, MetasoReaderResponse, MetasoReaderConfig>
{
    private const string ApiName = "秘塔网页阅读";

    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        "markdown",
        "json",
    };

    private readonly IMetasoClient _client;
    private MetasoReaderConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MetasoReaderPlugin"/> class.
    /// </summary>
    /// <param name="client">秘塔 API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public MetasoReaderPlugin(IMetasoClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Url": "https://www.163.com/news/article/xxx.html", // 要阅读的网页地址，http/https 开头
              "Format": "markdown"          // markdown（默认）| json（额外解析标题/作者/日期）
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
    public Task<string?> InitAsync(MetasoReaderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Task.FromResult<string?>("API Key 不能为空，请先在 https://metaso.cn/search-api/api-keys 获取");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<MetasoReaderResponse> RunAsync(MetasoReaderRequest request, CancellationToken cancellationToken)
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

        // markdown 走 text/plain 拿纯正文（Refit 字符串返回即响应体原文），json 走 application/json 拿结构化字段.
        var format = (request.Format ?? string.Empty).Trim();
        var asJson = Formats.TryGetValue(format, out var normalized) && normalized == "json";
        var authorization = MetasoAuthorization.Build(_config.ApiKey);
        var query = new MetasoReaderQuery { Url = url };

        try
        {
            if (asJson)
            {
                var response = await _client.ReadJsonAsync(authorization, query)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                return MapJson(response, url);
            }

            var markdown = await _client.ReadMarkdownAsync(authorization, query)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return new MetasoReaderResponse { Url = url, Content = markdown ?? string.Empty };
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 密钥无效、402 点数不足、422 目标站点不可抓取等）。
            throw new BusinessException((int)ex.StatusCode, $"{ApiName}调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }
    }

    private static MetasoReaderResponse MapJson(MetasoReaderResult? data, string requestUrl)
    {
        data ??= new MetasoReaderResult();
        return new MetasoReaderResponse
        {
            Url = string.IsNullOrWhiteSpace(data.Url) ? requestUrl : data.Url,
            Title = data.Title,
            Author = data.Author,
            Date = data.Date,
            Content = data.Markdown ?? data.Content ?? string.Empty,
            Credits = data.Credits,
        };
    }
}
