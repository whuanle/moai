using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Prometheus;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Prometheus 指标查询（动态插件）：用实例配置中的服务地址调用 Prometheus HTTP API v1，让 AI 执行 PromQL 或读取告警/规则做智能运维.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="PrometheusQueryRequest.Mode"/>）：instant / range / labels / label_values / series / alerts / rules。
/// 只读由所用端点保证（<c>/api/v1/query*</c>、labels、series、alerts、rules 都是读端点），时间与区间参数原样透传，
/// 解析层用 <see cref="ObservabilityJson"/> 容错展开；错误形态有二——Refit 非 2xx（401/403/429/5xx）与 HTTP 200 的
/// <c>status:"error"</c>（errorType/error），均归一为可直接展示的业务失败。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件；客户端为 transient，BaseAddress/超时按实例配置每次重设。
/// </remarks>
[AiPlugin(
    key: "prometheus_query",
    Name = "Prometheus 指标查询",
    Description = "查询 Prometheus 指标与告警：Mode 支持 instant（单点 PromQL）/range（区间）/labels（标签名）/label_values（标签取值）/series（标签集）/alerts（当前告警）/rules（规则组）；先以 {\"Mode\":\"labels\"} 探索可用标签，再用 instant {\"Query\":\"up\"} 起步")]
public class PrometheusQueryPlugin : IDynamicPluginRuntime<PrometheusQueryRequest, PrometheusQueryResponse, PrometheusQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>序列条数上限的下界.</summary>
    private const int MinMaxSeries = 1;

    /// <summary>序列条数上限的上界.</summary>
    private const int MaxMaxSeries = 200;

    /// <summary>每条序列采样点上限的下界.</summary>
    private const int MinMaxPoints = 1;

    /// <summary>每条序列采样点上限的上界.</summary>
    private const int MaxMaxPoints = 2000;

    /// <summary>列表条数上限的下界.</summary>
    private const int MinMaxListItems = 1;

    /// <summary>列表条数上限的上界.</summary>
    private const int MaxMaxListItems = 500;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "instant", "range", "labels", "label_values", "series", "alerts", "rules",
    };

    private readonly IPrometheusClient _client;
    private PrometheusQueryConfig _config = new();
    private bool _prepared;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Prometheus API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public PrometheusQueryPlugin(IPrometheusClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "instant",        // instant | range | labels | label_values | series | alerts | rules
              "Query": "up",            // instant/range 必填：PromQL 表达式
              "Time": null,             // instant 求值时间（RFC3339 或 Unix 秒），缺省当前时间
              "Start": null,            // range 开始时间，如 2026-09-26T00:00:00Z
              "End": null,              // range 结束时间，缺省当前时间
              "Step": "1m",             // range 步长持续时间字面量
              "Label": "job",           // label_values 模式：标签名
              "Selector": "job=node"    // series 模式：match[] 选择器
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://prometheus:9090", // Prometheus HTTP API 根地址，可含路径前缀
              "Username": "",                      // Basic 认证用户名（可选），与 BearerToken 都未填则匿名
              "Password": "",                      // Basic 认证密码（可选）
              "BearerToken": "",                   // Bearer Token（可选），已填时优先于用户名密码
              "TimeoutSeconds": 30,          // 单次请求超时秒数，1-300
              "MaxSeries": 50,               // 向量/矩阵最多返回序列条数，1-200
              "MaxPointsPerSeries": 500,     // 区间查询每条序列最多采样点，1-2000（保留最近 N 点）
              "MaxListItems": 200            // 列表类结果最多返回条数，1-500
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(PrometheusQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new PrometheusQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Username = config.Username.Trim(),
            Password = config.Password,
            BearerToken = config.BearerToken.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxSeries = Math.Clamp(config.MaxSeries, MinMaxSeries, MaxMaxSeries),
            MaxPointsPerSeries = Math.Clamp(config.MaxPointsPerSeries, MinMaxPoints, MaxMaxPoints),
            MaxListItems = Math.Clamp(config.MaxListItems, MinMaxListItems, MaxMaxListItems),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<PrometheusQueryResponse> RunAsync(PrometheusQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        var query = mode is "instant" or "range" ? RequireQuery(request.Query) : null;
        if (mode == "label_values" && string.IsNullOrWhiteSpace(request.Label))
        {
            throw new BusinessException(400, "label_values 模式需要提供标签名 Label");
        }

        if (mode == "series" && string.IsNullOrWhiteSpace(request.Selector))
        {
            throw new BusinessException(400, "series 模式需要提供标签集选择器 Selector（match[]），例如 job=node");
        }

        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, _config.BearerToken);
        PrepareClient();

        switch (mode)
        {
            case "instant":
                return ParsePromValue(await SendAsync(
                    () => _client.QueryAsync(authorization, query!, NullableTrim(request.Time), cancellationToken),
                    cancellationToken).ConfigureAwait(false));

            case "range":
                return ParsePromValue(await SendAsync(
                    () => _client.QueryRangeAsync(authorization, query!, NullableTrim(request.Start) ?? "-1h", NullableTrim(request.End) ?? "now", NullableTrim(request.Step) ?? "1m", cancellationToken),
                    cancellationToken).ConfigureAwait(false));

            case "labels":
                return ParseStringListResponse("labels",
                    await SendAsync(() => _client.LabelsAsync(authorization, cancellationToken), cancellationToken).ConfigureAwait(false));

            case "label_values":
                return ParseStringListResponse("label_values",
                    await SendAsync(() => _client.LabelValuesAsync(authorization, request.Label!.Trim(), cancellationToken), cancellationToken).ConfigureAwait(false));

            case "series":
                return ParseLabelSetsResponse(
                    await SendAsync(() => _client.SeriesAsync(authorization, request.Selector!.Trim(), cancellationToken), cancellationToken).ConfigureAwait(false));

            case "alerts":
                return ParseAlertsResponse(
                    await SendAsync(() => _client.AlertsAsync(authorization, cancellationToken), cancellationToken).ConfigureAwait(false));

            default:
                return ParseRulesResponse(
                    await SendAsync(() => _client.RulesAsync(authorization, cancellationToken), cancellationToken).ConfigureAwait(false));
        }
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "instant";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 instant/range/labels/label_values/series/alerts/rules");
        }

        return normalized;
    }

    private static string RequireQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new BusinessException(400, "PromQL 表达式 Query 不能为空");
        }

        return query.Trim();
    }

    private static string? NullableTrim(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 校验 BaseUrl.
    /// </summary>
    /// <param name="baseUrl">配置里的服务地址.</param>
    /// <returns>校验失败信息；通过返回 null.</returns>
    private static string? ValidateBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "服务地址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "服务地址 BaseUrl 必须是合法的 http:// 或 https:// URL";
        }

        return null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 每次执行前按实例配置设置 BaseAddress 与超时（客户端为 transient，运行之间互不干扰）；实例生命周期内只设一次（HttpClient 首请求后属性不可再改）.
    /// </summary>
    private void PrepareClient()
    {
        if (_prepared)
        {
            return;
        }

        _prepared = true;
        _client.Client.BaseAddress = new Uri(_config.BaseUrl);
        _client.Client.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    private async Task<string> SendAsync(Func<Task<string>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ex is ApiException apiException)
            {
                // Refit 对非 2xx 直接抛 ApiException；统一为带响应内容的业务异常，便于在运行抽屉中定位（401/403/429/5xx 等）.
                throw new BusinessException((int)apiException.StatusCode, $"Prometheus 调用失败（HTTP {(int)apiException.StatusCode}）：{apiException.Content ?? apiException.ReasonPhrase}");
            }

            throw;
        }
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    private static List<T> CapList<T>(List<T> source, int max, out bool truncated)
    {
        truncated = source.Count > max;
        return truncated ? source.GetRange(0, max) : source;
    }

    private PrometheusQueryResponse ParsePromValue(string raw)
    {
        using var document = ParseDocument(raw);
        var data = RequireSuccessAndData(document.RootElement);
        var resultType = ObservabilityJson.GetStringProperty(data, "resultType") ?? string.Empty;
        switch (resultType)
        {
            case "vector":
                var vector = ReadVector(GetArrayOrEmpty(data, "result"), out var vectorTruncated);
                return new PrometheusQueryResponse
                {
                    ResultType = resultType,
                    Vector = vector,
                    Truncated = vectorTruncated,
                };

            case "matrix":
                var matrix = ReadMatrix(GetArrayOrEmpty(data, "result"), out var matrixTruncated);
                return new PrometheusQueryResponse
                {
                    ResultType = resultType,
                    Matrix = matrix,
                    Truncated = matrixTruncated,
                };

            case "scalar" or "string":
                var scalar = data.TryGetProperty("result", out var scalarResult) && scalarResult.ValueKind == JsonValueKind.Array
                    ? scalarResult.Clone()
                    : throw new BusinessException(502, $"Prometheus 响应形态不是预期结果（resultType={resultType} 应为 [时间, 值]）");
                return new PrometheusQueryResponse
                {
                    ResultType = resultType,
                    Scalar = ReadScalarPair(scalar),
                };

            default:
                throw new BusinessException(502, $"Prometheus 响应 resultType={resultType} 不在 instant/range 查询的预期范围（vector/matrix/scalar/string）");
        }
    }

    private PrometheusScalarValue ReadScalarPair(JsonElement scalar)
    {
        var parts = new List<string>();
        foreach (var part in scalar.EnumerateArray())
        {
            parts.Add(part.ValueKind switch
            {
                JsonValueKind.String => part.GetString() ?? string.Empty,
                JsonValueKind.Number => part.GetRawText(),
                _ => string.Empty,
            });
            if (parts.Count == 2)
            {
                break;
            }
        }

        if (parts.Count != 2)
        {
            throw new BusinessException(502, "Prometheus 响应形态不是预期结果（scalar 应为 [时间, 值]）");
        }

        return new PrometheusScalarValue { Timestamp = parts[0], Value = parts[1] };
    }

    private IReadOnlyList<PrometheusSample> ReadVector(JsonElement result, out bool truncated)
    {
        var samples = new List<PrometheusSample>();
        truncated = false;
        if (result.ValueKind != JsonValueKind.Array)
        {
            return samples;
        }

        foreach (var item in result.EnumerateArray())
        {
            if (samples.Count >= _config.MaxSeries)
            {
                truncated = true;
                break;
            }

            var value = GetProperty(item, "value");
            samples.Add(new PrometheusSample
            {
                Labels = ObservabilityJson.ToStringMap(GetProperty(item, "metric")),
                Timestamp = ReadPairPart(value, 0),
                Value = ReadPairPart(value, 1),
            });
        }

        return samples;
    }

    private IReadOnlyList<PrometheusMatrixSeries> ReadMatrix(JsonElement result, out bool truncated)
    {
        var series = new List<PrometheusMatrixSeries>();
        truncated = false;
        if (result.ValueKind != JsonValueKind.Array)
        {
            return series;
        }

        foreach (var item in result.EnumerateArray())
        {
            if (series.Count >= _config.MaxSeries)
            {
                truncated = true;
                break;
            }

            var values = GetProperty(item, "values");
            var points = new List<PrometheusScalarValue>();
            if (values.ValueKind == JsonValueKind.Array)
            {
                foreach (var point in values.EnumerateArray())
                {
                    points.Add(new PrometheusScalarValue { Timestamp = ReadPairPart(point, 0), Value = ReadPairPart(point, 1) });
                }
            }

            var samples = points.Count > _config.MaxPointsPerSeries ? points.GetRange(points.Count - _config.MaxPointsPerSeries, _config.MaxPointsPerSeries) : points;
            series.Add(new PrometheusMatrixSeries
            {
                Labels = ObservabilityJson.ToStringMap(GetProperty(item, "metric")),
                Samples = samples,
            });
        }

        return series;
    }

    private static JsonElement GetArrayOrEmpty(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Array ? value : default;
    }

    private static string ReadPairPart(JsonElement pair, int index)
    {
        if (pair.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var current = 0;
        foreach (var part in pair.EnumerateArray())
        {
            if (current++ == index)
            {
                return part.ValueKind switch
                {
                    JsonValueKind.String => part.GetString() ?? string.Empty,
                    JsonValueKind.Number => part.GetRawText(),
                    _ => string.Empty,
                };
            }
        }

        return string.Empty;
    }

    private PrometheusQueryResponse ParseStringListResponse(string resultType, string raw)
    {
        using var document = ParseDocument(raw);
        var data = RequireSuccessAndData(document.RootElement);
        var items = new List<string>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    items.Add(item.GetString() ?? string.Empty);
                }
            }
        }

        var list = CapList(items, _config.MaxListItems, out var truncated);
        return new PrometheusQueryResponse { ResultType = resultType, Labels = list, Truncated = truncated };
    }

    private PrometheusQueryResponse ParseLabelSetsResponse(string raw)
    {
        using var document = ParseDocument(raw);
        var data = RequireSuccessAndData(document.RootElement);
        var labelSets = new List<IReadOnlyDictionary<string, string>>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var map = ObservabilityJson.ToStringMap(item);
                if (map.Count == 0)
                {
                    continue;
                }

                labelSets.Add(map);
            }
        }

        var series = CapList(labelSets, _config.MaxListItems, out var truncated);
        return new PrometheusQueryResponse { ResultType = "series", Series = series, Truncated = truncated };
    }

    private PrometheusQueryResponse ParseAlertsResponse(string raw)
    {
        using var document = ParseDocument(raw);
        var data = RequireSuccessAndData(document.RootElement);
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("alerts", out var alerts) || alerts.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(502, "Prometheus 响应形态不是预期结果（缺 alerts 数组）");
        }

        var parsed = new List<PrometheusAlert>();
        foreach (var alert in alerts.EnumerateArray())
        {
            parsed.Add(new PrometheusAlert
            {
                Labels = ObservabilityJson.ToStringMap(GetProperty(alert, "labels")),
                Annotations = ObservabilityJson.ToStringMap(GetProperty(alert, "annotations")),
                State = ObservabilityJson.GetStringProperty(alert, "state") ?? string.Empty,
                ActiveAt = ObservabilityJson.GetStringProperty(alert, "activeAt") ?? string.Empty,
                Value = ObservabilityJson.GetStringProperty(alert, "value") ?? string.Empty,
            });
        }

        var list = CapList(parsed, _config.MaxListItems, out var truncated);
        return new PrometheusQueryResponse { ResultType = "alerts", Alerts = list, Truncated = truncated };
    }

    private PrometheusQueryResponse ParseRulesResponse(string raw)
    {
        using var document = ParseDocument(raw);
        var data = RequireSuccessAndData(document.RootElement);
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(502, "Prometheus 响应形态不是预期结果（缺 groups 数组）");
        }

        var parsed = new List<PrometheusRuleGroup>();
        foreach (var group in groups.EnumerateArray())
        {
            parsed.Add(new PrometheusRuleGroup
            {
                File = ObservabilityJson.GetStringProperty(group, "file") ?? string.Empty,
                Name = ObservabilityJson.GetStringProperty(group, "name") ?? string.Empty,
                Rules = ReadRules(GetProperty(group, "rules")),
            });
        }

        var list = CapList(parsed, _config.MaxListItems, out var truncated);
        return new PrometheusQueryResponse { ResultType = "rules", Rules = list, Truncated = truncated };
    }

    private List<PrometheusRule> ReadRules(JsonElement rules)
    {
        var result = new List<PrometheusRule>();
        if (rules.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var rule in rules.EnumerateArray())
        {
            var hasAlert = rule.ValueKind == JsonValueKind.Object && rule.TryGetProperty("alert", out _);
            result.Add(new PrometheusRule
            {
                Name = ObservabilityJson.GetStringProperty(rule, "name") ?? string.Empty,
                Type = hasAlert ? "alerting" : "recording",
                Query = ObservabilityJson.GetStringProperty(rule, "query") ?? string.Empty,
                State = ObservabilityJson.GetStringProperty(rule, "state") ?? string.Empty,
                Health = ObservabilityJson.GetStringProperty(rule, "health") ?? string.Empty,
                LastError = ObservabilityJson.GetStringProperty(rule, "lastError") ?? string.Empty,
                Labels = ObservabilityJson.ToStringMap(GetProperty(rule, "labels")),
                Annotations = ObservabilityJson.ToStringMap(GetProperty(rule, "annotations")),
            });
        }

        return result;
    }

    private JsonDocument ParseDocument(string raw)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "Prometheus 响应不是合法 JSON，请检查服务地址是否正确");
        }
    }

    private static JsonElement RequireSuccessAndData(JsonElement root)
    {
        var status = ObservabilityJson.GetStringProperty(root, "status");
        if (!string.Equals(status, "success", StringComparison.Ordinal))
        {
            var errorType = ObservabilityJson.GetStringProperty(root, "errorType") ?? "unknown";
            var error = ObservabilityJson.GetStringProperty(root, "error") ?? "未知错误";
            throw new BusinessException(502, $"Prometheus 查询失败：{errorType}: {error}");
        }

        if (!root.TryGetProperty("data", out var data))
        {
            throw new BusinessException(502, "Prometheus 响应缺少 data 字段");
        }

        return data;
    }
}

