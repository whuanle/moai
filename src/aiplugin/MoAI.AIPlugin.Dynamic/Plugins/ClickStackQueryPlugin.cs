using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.ClickStack;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// ClickStack 查询（动态插件）：让 AI 经 HyperDX 对外 API 检索观测数据（日志/链路/指标时间线），补齐 ClickHouse 端口未公开时的排障入口.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="ClickStackQueryRequest.Mode"/>）：sources / search / chart。端点为
/// <c>api/v2/sources</c>、<c>api/v2/search</c>、<c>api/v2/charts/series</c>（API server 镜像默认 8000 端口，
/// 与 UI 8080 分离；BaseUrl 支持任意端口偏移与子路径部署）。鉴权固定 <c>Authorization: Bearer</c> +
/// Personal API Access Key（UI Team Settings → API Keys 创建，非 OTLP Ingestion Key）。
/// search/chart 的时间窗由插件归一（search 下发 ISO 秒精度，chart 下发 epoch 毫秒），缺省窗口
/// search=End-15 分钟、chart=End-1 小时；行数上限取 MaxResults 或配置 MaxRows，服务端硬上限 2000，
/// 返回行数达到上限即置 Truncated（用 Offset 翻页）。客户端为 <see cref="IClickStackClient"/>
/// （IHttpClientFactory 具名客户端，挂统一外部请求日志与遥测）。每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件。
/// </remarks>
[AiPlugin(
    key: "clickstack_query",
    Name = "ClickStack 查询",
    Description = "查询 ClickStack 观测数据（HyperDX 对外 API，日志/链路/指标）：Mode 支持 sources（列数据源拿 sourceId）/search（原始检索，默认）/chart（时间线聚合）；需 Personal API Access Key；先以 {\"Mode\":\"sources\"} 起步")]
public class ClickStackQueryPlugin : IDynamicPluginRuntime<ClickStackQueryRequest, ClickStackQueryResponse, ClickStackQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>默认行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>默认行数上限的上界.</summary>
    private const int MaxMaxRows = 500;

    /// <summary>服务端 maxResults 硬上限.</summary>
    private const int ServerMaxResults = 2000;

    /// <summary>分页偏移上限.</summary>
    private const int MaxOffset = 10000;

    /// <summary>where 表达式长度上限（与服务端一致）.</summary>
    private const int MaxWhereChars = 8192;

    /// <summary>select 表达式长度上限（与服务端一致）.</summary>
    private const int MaxSelectChars = 4096;

    /// <summary>orderBy 表达式长度上限（与服务端一致）.</summary>
    private const int MaxOrderByChars = 1024;

    /// <summary>search 缺省时间窗（分钟）.</summary>
    private const int SearchDefaultWindowMinutes = 15;

    /// <summary>chart 缺省时间窗（分钟）.</summary>
    private const int ChartDefaultWindowMinutes = 60;

    private const string SourcesRelativePath = "api/v2/sources";
    private const string SearchRelativePath = "api/v2/search";
    private const string ChartRelativePath = "api/v2/charts/series";

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "sources", "search", "chart",
    };

    private static readonly HashSet<string> WhereLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "lucene", "sql",
    };

    private static readonly HashSet<string> Granularities = new(StringComparer.Ordinal)
    {
        "30s", "1m", "5m", "10m", "15m", "30m", "1h", "2h", "6h", "12h", "1d", "2d", "7d", "30d", "auto",
    };

    private static readonly HashSet<string> AggFns = new(StringComparer.Ordinal)
    {
        "avg", "count", "count_distinct", "last_value", "max", "min", "quantile", "sum",
    };

    private readonly IClickStackClient _client;
    private ClickStackQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickStackQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">ClickStack 对外 API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public ClickStackQueryPlugin(IClickStackClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "search",      // sources | search | chart
              "SourceId": "<先 Mode=sources 获取>",
              "Where": "SeverityText:ERROR AND ServiceName:moai", // Lucene 过滤；WhereLanguage=sql 时改 SQL 片段
              "Select": "Timestamp,ServiceName,SeverityText,Body", // search 专属：返回列，可空=默认列
              "StartTime": "2026-09-29T00:00:00Z", // 可空=End-15分钟（chart 为 End-1小时）
              "EndTime": "2026-09-29T01:00:00Z",   // 可空=now
              "MaxResults": 50,      // search 专属：可空=配置 MaxRows
              "Granularity": "5m",   // chart 专属：聚合粒度，可空=1h
              "AggFn": "count",      // chart 专属：聚合函数，可空=count
              "GroupBy": "ServiceName" // chart 专属：分组字段（逗号分隔），可空
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://192.168.50.199:28000", // ClickStack 对外 API 基址（API server，镜像默认 8000 端口，与 UI 8080 分离）
              "ApiKey": "",            // Personal API Access Key：HyperDX UI → Team Settings → API Keys 创建（非 OTLP Ingestion Key）
              "TimeoutSeconds": 30,    // 单次请求超时秒数，1-300
              "MaxRows": 100           // search 默认行数上限，1-500（服务端硬上限 2000）
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(ClickStackQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new ClickStackQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            ApiKey = config.ApiKey.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<ClickStackQueryResponse> RunAsync(ClickStackQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        if (_config.ApiKey.Length == 0)
        {
            throw new BusinessException(400, "需要在实例配置中提供 ApiKey（Personal API Access Key，HyperDX UI → Team Settings → API Keys 创建；与 OTLP 摄入的 Ingestion Key 不同）");
        }

        var authorization = $"Bearer {_config.ApiKey}";
        return mode switch
        {
            "sources" => await SourcesAsync(authorization, cancellationToken).ConfigureAwait(false),
            "chart" => await ChartAsync(request, authorization, cancellationToken).ConfigureAwait(false),
            _ => await SearchAsync(request, authorization, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "search";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 sources/search/chart");
        }

        return normalized;
    }

    /// <summary>
    /// 校验 BaseUrl.
    /// </summary>
    /// <param name="baseUrl">配置里的 API 基址.</param>
    /// <returns>校验失败信息；通过返回 null.</returns>
    private static string? ValidateBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "ClickStack API 基址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "ClickStack API 基址 BaseUrl 必须是合法的 http:// 或 https:// URL";
        }

        return null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 归一 where 语言.
    /// </summary>
    /// <param name="whereLanguage">请求原文.</param>
    /// <returns>归一后的小写值；非法抛 400.</returns>
    private static string NormalizeWhereLanguage(string? whereLanguage)
    {
        var normalized = (whereLanguage ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return "lucene";
        }

        if (!WhereLanguages.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 WhereLanguage={normalized}：仅允许 lucene/sql");
        }

        return normalized.ToLowerInvariant();
    }

    /// <summary>
    /// 归一 chart 聚合函数.
    /// </summary>
    /// <param name="aggFn">请求原文.</param>
    /// <returns>归一后的小写值；空默认 count；非法抛 400.</returns>
    private static string NormalizeAggFn(string? aggFn)
    {
        var normalized = (aggFn ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "count";
        }

        if (!AggFns.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 AggFn={normalized}：仅允许 {string.Join("/", AggFns)}");
        }

        return normalized;
    }

    /// <summary>
    /// 归一 chart 聚合粒度.
    /// </summary>
    /// <param name="granularity">请求原文.</param>
    /// <returns>归一后的小写值；空默认 1h；非法抛 400.</returns>
    private static string NormalizeGranularity(string? granularity)
    {
        var normalized = (granularity ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "1h";
        }

        if (!Granularities.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Granularity={normalized}：仅允许 {string.Join("/", Granularities)}");
        }

        return normalized;
    }

    /// <summary>
    /// 解析时间窗终点；未填默认当前时间.
    /// </summary>
    /// <param name="input">请求原文.</param>
    /// <param name="fieldName">字段名（用于报错）.</param>
    /// <returns>UTC 时间.</returns>
    private static DateTimeOffset ParseTimeOrThrow(string? input, string fieldName)
    {
        if (!ClickStackResponseParser.TryParseTime(input, out var time))
        {
            throw new BusinessException(400, $"{fieldName} 无法识别为时间（示例 2026-09-29T00:00:00Z 或 2026-09-29 08:00:00），收到「{input?.Trim()}」");
        }

        return time;
    }

    /// <summary>
    /// 校验表达式长度（与服务端限制对齐，提前给出可读错误）.
    /// </summary>
    private static void EnsureLength(string value, int maxChars, string fieldName)
    {
        if (value.Length > maxChars)
        {
            throw new BusinessException(400, $"{fieldName} 超过 {maxChars} 字符上限（当前 {value.Length}），请缩短表达式或收紧时间窗");
        }
    }

    private async Task<ClickStackQueryResponse> SourcesAsync(string authorization, CancellationToken cancellationToken)
    {
        var root = await SendAsync(HttpMethod.Get, SourcesRelativePath, null, authorization, cancellationToken).ConfigureAwait(false);
        return new ClickStackQueryResponse { ResultType = "sources", Sources = ClickStackResponseParser.ParseSources(root) };
    }

    private async Task<ClickStackQueryResponse> SearchAsync(ClickStackQueryRequest request, string authorization, CancellationToken cancellationToken)
    {
        var sourceId = request.SourceId.Trim();
        if (sourceId.Length == 0)
        {
            throw new BusinessException(400, "search 模式需要 SourceId（先以 Mode=sources 查询可用数据源）");
        }

        var whereLanguage = NormalizeWhereLanguage(request.WhereLanguage);
        var where = request.Where?.Trim() ?? string.Empty;
        var select = request.Select?.Trim() ?? string.Empty;
        var orderBy = request.OrderBy?.Trim() ?? string.Empty;
        EnsureLength(where, MaxWhereChars, "Where");
        EnsureLength(select, MaxSelectChars, "Select");
        EnsureLength(orderBy, MaxOrderByChars, "OrderBy");

        var limit = request.MaxResults > 0
            ? Math.Clamp(request.MaxResults, 1, ServerMaxResults)
            : _config.MaxRows;
        var offset = Math.Clamp(request.Offset, 0, MaxOffset);

        var end = string.IsNullOrWhiteSpace(request.EndTime) ? DateTimeOffset.UtcNow : ParseTimeOrThrow(request.EndTime, "EndTime");
        var start = string.IsNullOrWhiteSpace(request.StartTime) ? end.AddMinutes(-SearchDefaultWindowMinutes) : ParseTimeOrThrow(request.StartTime, "StartTime");

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sourceId"] = sourceId,
            ["startTime"] = start.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["endTime"] = end.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["where"] = where,
            ["whereLanguage"] = whereLanguage,
            ["maxResults"] = limit,
            ["offset"] = offset,
        };
        if (select.Length > 0)
        {
            body["select"] = select;
        }

        if (orderBy.Length > 0)
        {
            body["orderBy"] = orderBy;
        }

        var root = await SendAsync(HttpMethod.Post, SearchRelativePath, JsonSerializer.Serialize(body), authorization, cancellationToken).ConfigureAwait(false);
        var rows = ClickStackResponseParser.ParseSearchRows(root);
        var rowCount = ClickStackResponseParser.GetSearchRowCount(root, rows.Count);
        return new ClickStackQueryResponse
        {
            ResultType = "search",
            Rows = rows,
            RowCount = rowCount,
            Truncated = rowCount >= limit,
        };
    }

    private async Task<ClickStackQueryResponse> ChartAsync(ClickStackQueryRequest request, string authorization, CancellationToken cancellationToken)
    {
        var sourceId = request.SourceId.Trim();
        if (sourceId.Length == 0)
        {
            throw new BusinessException(400, "chart 模式需要 SourceId（先以 Mode=sources 查询可用数据源）");
        }

        var aggFn = NormalizeAggFn(request.AggFn);
        var field = request.Field?.Trim() ?? string.Empty;
        if (aggFn != "count" && field.Length == 0)
        {
            throw new BusinessException(400, $"AggFn={aggFn} 需要提供 Field（count 可省略）");
        }

        var granularity = NormalizeGranularity(request.Granularity);
        var whereLanguage = NormalizeWhereLanguage(request.WhereLanguage);
        var where = request.Where?.Trim() ?? string.Empty;
        EnsureLength(where, MaxWhereChars, "Where");

        var groupBy = (request.GroupBy ?? string.Empty)
            .Split(',')
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();

        var end = string.IsNullOrWhiteSpace(request.EndTime) ? DateTimeOffset.UtcNow : ParseTimeOrThrow(request.EndTime, "EndTime");
        var start = string.IsNullOrWhiteSpace(request.StartTime) ? end.AddMinutes(-ChartDefaultWindowMinutes) : ParseTimeOrThrow(request.StartTime, "StartTime");

        var series = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sourceId"] = sourceId,
            ["aggFn"] = aggFn,
            ["where"] = where,
            ["groupBy"] = groupBy,
        };
        if (field.Length > 0)
        {
            series["field"] = field;
        }

        if (whereLanguage == "sql")
        {
            series["whereLanguage"] = "sql";
        }

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["startTime"] = start.ToUnixTimeMilliseconds(),
            ["endTime"] = end.ToUnixTimeMilliseconds(),
            ["granularity"] = granularity,
            ["series"] = new List<object?> { series },
        };

        var root = await SendAsync(HttpMethod.Post, ChartRelativePath, JsonSerializer.Serialize(body), authorization, cancellationToken).ConfigureAwait(false);
        return new ClickStackQueryResponse { ResultType = "chart", Points = ClickStackResponseParser.ParseChartPoints(root) };
    }

    /// <summary>
    /// 发送一次 API 调用并解析 JSON；超时/HTTP 状态/非 JSON 归一为可读业务异常.
    /// </summary>
    private async Task<JsonElement> SendAsync(HttpMethod method, string relativePath, string? jsonBody, string authorization, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(_config.BaseUrl), relativePath);
        string raw;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            raw = method == HttpMethod.Get
                ? await _client.GetAsync(endpoint, authorization, timeoutSource.Token).ConfigureAwait(false)
                : await _client.PostJsonAsync(endpoint, authorization, jsonBody!, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(502, $"ClickStack 请求超时（{_config.TimeoutSeconds}s），请检查 API 基址与网络");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            var status = (int)ex.StatusCode.Value;
            throw new BusinessException(status, $"{DescribeHttpStatus(status, ex.Message)}：{ex.Message}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"ClickStack 调用失败：{ex.Message}");
        }

        using var document = ObservabilityJson.ParseOrThrow(raw, "ClickStack");
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 按状态码给出可读提示（正文细节由客户端异常消息携带；Express 形态的 Cannot POST 404 说明端点在对面不存在而非 SourceId 无效）.
    /// </summary>
    private static string DescribeHttpStatus(int status, string detail)
    {
        if (status == 404 && detail.Contains("Cannot ", StringComparison.OrdinalIgnoreCase))
        {
            return "ClickStack 端点不存在（对面返回了 Express 路由 404）：若 BaseUrl 已指向 API server，多为镜像版本过旧缺少该端点（Search 需较新版 ClickStack，建议升级 clickstack-app 镜像）；若指向的是 UI 端口请改指 API server 端口";
        }

        return status switch
        {
            400 => "ClickStack 拒绝请求（参数不合法）",
            401 or 403 => $"ClickStack 鉴权失败（HTTP {status}）：ApiKey 需为 Personal API Access Key（HyperDX UI → Team Settings → API Keys 创建），不是 OTLP Ingestion Key",
            404 => "ClickStack 资源不存在（检查 SourceId 是否有效、BaseUrl 是否指向 API server）",
            _ => $"ClickStack 调用失败（HTTP {status}）",
        };
    }
}
