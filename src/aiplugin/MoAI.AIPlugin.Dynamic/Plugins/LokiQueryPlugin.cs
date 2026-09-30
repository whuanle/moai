using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Loki;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Loki 日志查询（动态插件）：让 AI 用 LogQL 检索日志流，补齐云原生栈日志腿（与 Prometheus/Tempo 同栈）.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="LokiQueryRequest.Mode"/>）：query / query_range / labels / label_values / series。
/// 只读由所用端点保证（<c>/loki/api/v1/query*</c>、labels、series 均为读端点）；时间参数归一为 Loki 的
/// Unix 纳秒（<see cref="LokiTimeHelper"/>）；行数上限下发 <c>limit</c> 并在客户端再截断（按流逐行）。
/// 鉴权经 <see cref="OpsAuthorization"/>；端点按实例 BaseUrl（支持子路径）拼装，客户端为
/// <see cref="ILokiClient"/>。每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件。
/// </remarks>
[AiPlugin(
    key: "loki_query",
    Name = "Loki 日志查询",
    Description = "用 LogQL 查询 Grafana Loki 日志：Mode 支持 query（即时）/query_range（区间）/labels（标签名）/label_values（标签取值）/series（标签集）；先以 {\"Mode\":\"labels\"} 探索，再 query {\"Query\":\"{job=\\\"nginx\\\"} |= \\\"error\\\"\"} 起步")]
public class LokiQueryPlugin : IDynamicPluginRuntime<LokiQueryRequest, LokiQueryResponse, LokiQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "query", "query_range", "labels", "label_values", "series",
    };

    private readonly ILokiClient _client;
    private LokiQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LokiQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Loki API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public LokiQueryPlugin(ILokiClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "query_range",  // query | query_range | labels | label_values | series
              "Query": "{job=\"nginx\"} |= \"error\"", // LogQL（query/query_range 必填）
              "Start": "2026-09-29T00:00:00Z", // query_range：开始时间（RFC3339 或 Unix 秒）
              "End": null,            // query_range：结束时间，缺省当前
              "Direction": "backward", // backward=最新在前（默认）/ forward
              "Label": "job",         // label_values：标签名
              "Selector": "{job=\"nginx\"}" // series：match[] 选择器（必填）
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://loki:3100", // Loki 地址，支持子路径如 http://host/loki
              "Username": "",          // Basic 用户名（可选）
              "Password": "",          // Basic 密码（可选）
              "BearerToken": "",       // Bearer Token（可选），已填时优先于用户名密码
              "TimeoutSeconds": 30,    // 单次请求超时秒数，1-300
              "MaxLines": 200,         // 日志行总数上限，1-2000
              "MaxLineChars": 1024,    // 单行最大字符数，128-4096
              "MaxListItems": 200      // 标签/序列列表最多返回条数，1-500
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(LokiQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new LokiQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Username = config.Username.Trim(),
            Password = config.Password,
            BearerToken = config.BearerToken.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxLines = Math.Clamp(config.MaxLines, 1, 2000),
            MaxLineChars = Math.Clamp(config.MaxLineChars, 128, 4096),
            MaxListItems = Math.Clamp(config.MaxListItems, 1, 500),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<LokiQueryResponse> RunAsync(LokiQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request);
        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, _config.BearerToken);
        return mode switch
        {
            "query_range" => await StreamsAsync(BuildQueryRangePath(request), authorization, cancellationToken).ConfigureAwait(false),
            "labels" => await StringListAsync("labels", $"api/v1/labels", authorization, cancellationToken, "labels").ConfigureAwait(false),
            "label_values" => await StringListAsync(
                "label_values",
                $"api/v1/label/{Uri.EscapeDataString(request.Label!.Trim())}/values",
                authorization,
                cancellationToken,
                "label_values").ConfigureAwait(false),
            "series" => await SeriesAsync(request, authorization, cancellationToken).ConfigureAwait(false),
            _ => await StreamsAsync(BuildQueryPath(request), authorization, cancellationToken).ConfigureAwait(false),
        };
    }

    private string NormalizeMode(LokiQueryRequest request)
    {
        var normalized = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "query";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 query/query_range/labels/label_values/series");
        }

        if (normalized is "query" or "query_range" && string.IsNullOrWhiteSpace(request.Query))
        {
            throw new BusinessException(400, "LogQL 表达式 Query 不能为空（query/query_range 模式必填）");
        }

        if (normalized == "label_values" && string.IsNullOrWhiteSpace(request.Label))
        {
            throw new BusinessException(400, "label_values 模式需要提供标签名 Label");
        }

        if (normalized == "series" && string.IsNullOrWhiteSpace(request.Selector))
        {
            throw new BusinessException(400, "series 模式需要提供 match[] 选择器 Selector，例如 {job=\"nginx\"}");
        }

        return normalized;
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
            return "Loki 服务地址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Loki 服务地址 BaseUrl 必须是合法的 http:// 或 https:// URL";
        }

        return null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 拼查询串（键值均转义）.
    /// </summary>
    private static string BuildQuery(IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        var parts = new List<string>();
        foreach (var pair in parameters)
        {
            parts.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private string BuildQueryPath(LokiQueryRequest request)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("query", request.Query.Trim()),
            new("limit", _config.MaxLines.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        var time = LokiTimeHelper.ToNanoSeconds(request.Time);
        if (time != null)
        {
            parameters.Add(new KeyValuePair<string, string>("time", time));
        }

        return $"api/v1/query{BuildQuery(parameters)}";
    }

    private string BuildQueryRangePath(LokiQueryRequest request)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("query", request.Query.Trim()),
            new("limit", _config.MaxLines.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("direction", string.Equals(request.Direction?.Trim(), "forward", StringComparison.Ordinal) ? "forward" : "backward"),
        };
        parameters.Add(new KeyValuePair<string, string>(
            "start",
            LokiTimeHelper.ToNanoSeconds(request.Start) ?? (DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds() * 1_000_000L).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        parameters.Add(new KeyValuePair<string, string>(
            "end",
            LokiTimeHelper.ToNanoSeconds(request.End) ?? (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"api/v1/query_range{BuildQuery(parameters)}";
    }

    private async Task<JsonElement> SendAsync(string relativePathWithQuery, string? authorization, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(_config.BaseUrl), relativePathWithQuery);
        string raw;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            raw = await _client.GetAsync(endpoint, authorization, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(502, $"Loki 请求超时（{_config.TimeoutSeconds}s），请检查服务地址与网络");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new BusinessException((int)ex.StatusCode.Value, $"Loki 调用失败（HTTP {(int)ex.StatusCode.Value}）：{ex.Message}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Loki 调用失败：{ex.Message}");
        }

        try
        {
            var root = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw).RootElement;
            var status = ObservabilityJson.GetStringProperty(root, "status");
            if (!string.Equals(status, "success", StringComparison.Ordinal))
            {
                var error = ObservabilityJson.GetStringProperty(root, "error") ?? "未知错误";
                throw new BusinessException(502, $"Loki 查询失败：{error}");
            }

            if (!root.TryGetProperty("data", out var data))
            {
                throw new BusinessException(502, "Loki 响应缺少 data 字段");
            }

            return data.Clone();
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "Loki 响应不是合法 JSON，请检查 BaseUrl 是否指向 Loki");
        }
    }

    private async Task<LokiQueryResponse> StreamsAsync(string relativePathWithQuery, string? authorization, CancellationToken cancellationToken)
    {
        var data = await SendAsync(relativePathWithQuery, authorization, cancellationToken).ConfigureAwait(false);
        var streams = new List<LokiStream>();
        var totalLines = 0;
        var truncated = false;
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in result.EnumerateArray())
            {
                var lines = new List<LokiLine>();
                if (stream.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pair in values.EnumerateArray())
                    {
                        if (totalLines >= _config.MaxLines)
                        {
                            truncated = true;
                            break;
                        }

                        string? timestampNs = null;
                        var text = string.Empty;
                        if (pair.ValueKind == JsonValueKind.Array)
                        {
                            var index = 0;
                            foreach (var part in pair.EnumerateArray())
                            {
                                if (index++ == 0)
                                {
                                    timestampNs = part.ValueKind == JsonValueKind.String ? part.GetString() : part.GetRawText();
                                }
                                else
                                {
                                    text = part.ValueKind == JsonValueKind.String ? part.GetString() ?? string.Empty : part.GetRawText();
                                    break;
                                }
                            }
                        }

                        lines.Add(new LokiLine
                        {
                            Timestamp = LokiTimeHelper.NanoSecondsToIso(timestampNs),
                            Text = ObservabilityJson.Truncate(text, _config.MaxLineChars),
                        });
                        totalLines++;
                    }
                }

                streams.Add(new LokiStream
                {
                    Labels = ObservabilityJson.ToStringMap(GetProperty(stream, "stream")),
                    Lines = lines,
                });
            }
        }

        return new LokiQueryResponse { ResultType = "streams", Streams = streams, Truncated = truncated };
    }

    private async Task<LokiQueryResponse> StringListAsync(string resultType, string relativePath, string? authorization, CancellationToken cancellationToken, string dataKey)
    {
        var data = await SendAsync(relativePath, authorization, cancellationToken).ConfigureAwait(false);
        var items = new List<string>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (items.Count >= _config.MaxListItems)
                {
                    break;
                }

                if (item.ValueKind == JsonValueKind.String)
                {
                    items.Add(item.GetString() ?? string.Empty);
                }
            }
        }

        var truncated = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > _config.MaxListItems;
        var response = new LokiQueryResponse { ResultType = resultType, Truncated = truncated };
        if (resultType == "labels")
        {
            response.Labels = items;
        }
        else
        {
            response.LabelValues = items;
        }

        return response;
    }

    private async Task<LokiQueryResponse> SeriesAsync(LokiQueryRequest request, string? authorization, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>> { new("match[]", request.Selector!.Trim()) };
        var data = await SendAsync($"api/v1/series{BuildQuery(parameters)}", authorization, cancellationToken).ConfigureAwait(false);
        var series = new List<IReadOnlyDictionary<string, string>>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (series.Count >= _config.MaxListItems)
                {
                    break;
                }

                var map = ObservabilityJson.ToStringMap(item);
                if (map.Count > 0)
                {
                    series.Add(map);
                }
            }
        }

        var truncated = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > _config.MaxListItems;
        return new LokiQueryResponse { ResultType = "series", Series = series, Truncated = truncated };
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }
}
