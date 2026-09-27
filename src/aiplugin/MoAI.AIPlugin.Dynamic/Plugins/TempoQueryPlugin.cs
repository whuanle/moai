using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Tempo;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Grafana Tempo 链路查询（动态插件）：用实例配置中的服务地址执行 TraceQL 检索/取链路详情/发现标签，供 AI 做分布式排障.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="TempoQueryRequest.Mode"/>）：traceql / trace / tags / tag_values。
/// 只读由所用端点保证（均为查询读端点）；解析层用 <see cref="ObservabilityJson"/> 容错展开——属性同时兼容
/// OTel JSON（<c>[{key,value:{stringValue|intVal...}}]</c> 数组）与平对象两种形态，时长缺失时按
/// endTimeUnixNano - startTimeUnixNano 求算；Refit 非 2xx 归一为带状态码与响应体的业务失败；
/// 链路详情按 MaxSpans 截断。每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件；
/// 客户端为 transient，BaseAddress/超时按实例配置每次重设。
/// </remarks>
[AiPlugin(
    key: "tempo_query",
    Name = "Grafana Tempo 链路查询",
    Description = "查询 Grafana Tempo 分布式链路：Mode 支持 traceql（TraceQL 检索）/trace（按 traceID 取详情）/tags（可检索标签）/tag_values（标签取值，默认 service.name 列服务）；先 {\"Mode\":\"tag_values\"} 列服务，再用 {\"Mode\":\"traceql\",\"Query\":\"{service.name=\\\"api\\\" && duration>1s}\"} 检索，最后按 TraceId 取详情")]
public class TempoQueryPlugin : IDynamicPluginRuntime<TempoQueryRequest, TempoQueryResponse, TempoQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>检索链路数上限的下界.</summary>
    private const int MinMaxTraces = 1;

    /// <summary>检索链路数上限的上界.</summary>
    private const int MaxMaxTraces = 100;

    /// <summary>单链路 span 展开上限的下界.</summary>
    private const int MinMaxSpans = 1;

    /// <summary>单链路 span 展开上限的上界.</summary>
    private const int MaxMaxSpans = 1000;

    /// <summary>tag_values 模式的默认标签.</summary>
    private const string DefaultTag = "service.name";

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "traceql", "trace", "tags", "tag_values",
    };

    private readonly ITempoClient _client;
    private TempoQueryConfig _config = new();
    private bool _prepared;

    /// <summary>
    /// Initializes a new instance of the <see cref="TempoQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Tempo API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public TempoQueryPlugin(ITempoClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "traceql",   // traceql | trace | tags | tag_values
              "Query": "{service.name=\"api\" && duration>1s}", // traceql：TraceQL 表达式
              "Limit": 20,         // traceql：最多返回链路数，1-100
              "Start": null,       // traceql：开始时间（RFC3339 / Unix 秒）
              "End": null,         // traceql：结束时间
              "TraceId": "1234aaaa5678bbb", // trace：目标 TraceID
              "Tag": "service.name" // tag_values：标签名，默认 service.name
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://tempo:3200", // Tempo 服务地址，可含路径前缀
              "Username": "",                 // Basic 认证用户名（可选），与 BearerToken 都未填则匿名
              "Password": "",                 // Basic 认证密码（可选）
              "BearerToken": "",              // Bearer Token（可选），已填时优先于用户名密码
              "TenantId": "",                 // 租户 id（可选）：随 x-scope-orgid 头下发
              "TimeoutSeconds": 30,           // 单次请求超时秒数，1-300
              "MaxTraces": 20,                // TraceQL 检索最多返回链路数，1-100
              "MaxSpans": 200                 // 取链路详情最多展开 span 数，1-1000
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(TempoQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new TempoQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Username = config.Username.Trim(),
            Password = config.Password,
            BearerToken = config.BearerToken.Trim(),
            TenantId = config.TenantId.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxTraces = Math.Clamp(config.MaxTraces, MinMaxTraces, MaxMaxTraces),
            MaxSpans = Math.Clamp(config.MaxSpans, MinMaxSpans, MaxMaxSpans),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<TempoQueryResponse> RunAsync(TempoQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode.Length == 0)
        {
            mode = "traceql";
        }

        if (!Modes.Contains(mode))
        {
            throw new BusinessException(400, $"不支持的 Mode={mode}：仅允许 traceql/trace/tags/tag_values");
        }

        if (mode == "traceql")
        {
            RequireQuery(request.Query);
        }
        else if (mode == "trace")
        {
            RequireTraceId(request.TraceId);
        }

        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, _config.BearerToken);
        PrepareClient();
        var tenantId = NullableTrim(_config.TenantId);

        try
        {
            return mode switch
            {
                "traceql" => await RunTraceqlAsync(authorization, tenantId, request, cancellationToken).ConfigureAwait(false),
                "trace" => await RunTraceAsync(authorization, tenantId, request.TraceId, cancellationToken).ConfigureAwait(false),
                "tags" => ParseTagsResponse(
                    await _client.TagsAsync(authorization, tenantId, cancellationToken).ConfigureAwait(false)),
                _ => await RunTagValuesAsync(authorization, tenantId, request.Tag, cancellationToken).ConfigureAwait(false),
            };
        }
        catch (ApiException ex)
        {
            var extra = mode == "trace" ? "（TraceID 不存在通常会返回 HTTP 404）" : string.Empty;
            throw new BusinessException((int)ex.StatusCode, $"Tempo 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}{extra}");
        }
    }

    private static string RequireQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new BusinessException(400, "traceql 模式的 TraceQL 表达式 Query 不能为空");
        }

        return query.Trim();
    }

    private static string RequireTraceId(string? traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new BusinessException(400, "trace 模式需要提供 TraceId");
        }

        return traceId.Trim();
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

    private static string? NullableTrim(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<TempoQueryResponse> RunTraceqlAsync(string? authorization, string? tenantId, TempoQueryRequest request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit ?? _config.MaxTraces, MinMaxTraces, _config.MaxTraces);
        var raw = await _client.SearchAsync(
            authorization,
            tenantId,
            RequireQuery(request.Query),
            limit,
            NullableTrim(request.Start),
            NullableTrim(request.End),
            cancellationToken).ConfigureAwait(false);
        return ParseSearchResponse(raw);
    }

    private async Task<TempoQueryResponse> RunTraceAsync(string? authorization, string? tenantId, string? traceId, CancellationToken cancellationToken)
    {
        var raw = await _client.GetTraceAsync(authorization, tenantId, "application/json", RequireTraceId(traceId), cancellationToken).ConfigureAwait(false);
        return ParseTraceResponse(raw);
    }

    private async Task<TempoQueryResponse> RunTagValuesAsync(string? authorization, string? tenantId, string? tag, CancellationToken cancellationToken)
    {
        var tagName = string.IsNullOrWhiteSpace(tag) ? DefaultTag : tag.Trim();
        var raw = await _client.TagValuesAsync(authorization, tenantId, tagName, cancellationToken).ConfigureAwait(false);
        return new TempoQueryResponse
        {
            Mode = "tag_values",
            TagValues = ParseStringListResponse(raw, "tagValues"),
        };
    }

    private TempoQueryResponse ParseTagsResponse(string raw)
    {
        return new TempoQueryResponse
        {
            Mode = "tags",
            Tags = ParseStringListResponse(raw, "tagNames"),
        };
    }

    /// <summary>
    /// 解析字符串数组形态的响应（tags/tagValues；放不下非字符串取值的条目被丢弃）.
    /// </summary>
    private static List<string> ParseStringListResponse(string raw, string propertyName)
    {
        using var document = ObservabilityJson.ParseOrThrow(raw, "Tempo");
        var root = document.RootElement;
        var items = new List<string>();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    items.Add(item.GetString() ?? string.Empty);
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    items.Add(item.GetString() ?? string.Empty);
                }
            }
        }

        return items;
    }

    private TempoQueryResponse ParseSearchResponse(string raw)
    {
        using var document = ObservabilityJson.ParseOrThrow(raw, "Tempo");
        var root = document.RootElement;
        var traces = new List<TempoTraceSummary>();
        var total = 0;
        if (root.TryGetProperty("traces", out var traceItems) && traceItems.ValueKind == JsonValueKind.Array)
        {
            total = traceItems.GetArrayLength();
            foreach (var trace in traceItems.EnumerateArray())
            {
                if (traces.Count >= _config.MaxTraces)
                {
                    break;
                }

                traces.Add(new TempoTraceSummary
                {
                    TraceId = ObservabilityJson.GetStringProperty(trace, "traceID") ?? string.Empty,
                    RootServiceName = ObservabilityJson.GetStringProperty(trace, "rootServiceName") ?? string.Empty,
                    RootTraceName = ObservabilityJson.GetStringProperty(trace, "rootTraceName") ?? string.Empty,
                    StartTimeUnixNano = ObservabilityJson.GetStringProperty(trace, "startTimeUnixNano") ?? string.Empty,
                    DurationMs = ObservabilityJson.GetStringProperty(trace, "durationMs") ?? string.Empty,
                });
            }
        }

        return new TempoQueryResponse
        {
            Mode = "traceql",
            Traces = traces,
            Truncated = traces.Count < total,
        };
    }

    private TempoQueryResponse ParseTraceResponse(string raw)
    {
        using var document = ObservabilityJson.ParseOrThrow(raw, "Tempo");
        var root = document.RootElement;
        var batches = new List<TempoBatch>();
        var truncated = false;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("batches", out var batchItems) && batchItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var batch in batchItems.EnumerateArray())
            {
                var spans = new List<TempoSpan>();
                if (batch.TryGetProperty("scopeSpans", out var scopeSpans) && scopeSpans.ValueKind == JsonValueKind.Array)
                {
                    foreach (var scope in scopeSpans.EnumerateArray())
                    {
                        if (!scope.TryGetProperty("spans", out var spanItems) || spanItems.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var span in spanItems.EnumerateArray())
                        {
                            if (spans.Count >= _config.MaxSpans)
                            {
                                truncated = true;
                                break;
                            }

                            spans.Add(ReadSpan(span));
                        }

                        if (truncated)
                        {
                            break;
                        }
                    }
                }

                batches.Add(new TempoBatch
                {
                    Resource = ObservabilityJson.ToOtelMap(GetPropertyOrUndefined(batch, "resource")),
                    Spans = spans,
                });

                if (truncated)
                {
                    break;
                }
            }
        }

        return new TempoQueryResponse
        {
            Mode = "trace",
            Batches = batches,
            Truncated = truncated,
        };
    }

    /// <summary>
    /// batch → resource 属性节点（先进入 resource.attributes）.
    /// </summary>
    /// <param name="parent">父对象.</param>
    /// <param name="name">属性名.</param>
    /// <returns>克隆后的子元素；缺失时为 Undefined.</returns>
    private static JsonElement GetPropertyOrUndefined(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("attributes", out var attributes))
            {
                return attributes.Clone();
            }

            return value.Clone();
        }

        return default;
    }

    private TempoSpan ReadSpan(JsonElement span)
    {
        var startTime = ObservabilityJson.GetStringProperty(span, "startTimeUnixNano") ?? string.Empty;
        var duration = ObservabilityJson.GetStringProperty(span, "durationUnixNano");
        if (string.IsNullOrWhiteSpace(duration))
        {
            duration = ReadDurationFallback(span, startTime);
        }

        var status = GetPropertyOrUndefined(span, "status");
        return new TempoSpan
        {
            SpanId = ObservabilityJson.GetStringProperty(span, "spanID") ?? string.Empty,
            ParentSpanId = ObservabilityJson.GetStringProperty(span, "parentSpanID") ?? string.Empty,
            Name = ObservabilityJson.GetStringProperty(span, "name") ?? string.Empty,
            StartTimeUnixNano = startTime,
            DurationNanos = duration ?? string.Empty,
            StatusCode = ObservabilityJson.GetStringProperty(status, "code") ?? string.Empty,
            StatusMessage = ObservabilityJson.GetStringProperty(status, "message") ?? string.Empty,
            Attributes = ReadSpanAttributes(span),
        };
    }

    private static string? ReadDurationFallback(JsonElement span, string startTime)
    {
        var endTime = ObservabilityJson.GetStringProperty(span, "endTimeUnixNano");
        if (string.IsNullOrWhiteSpace(endTime) || string.IsNullOrWhiteSpace(startTime)
            || !long.TryParse(endTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end)
            || !long.TryParse(startTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
        {
            return string.Empty;
        }

        return Math.Max(0, end - start).ToString(CultureInfo.InvariantCulture);
    }

    private IReadOnlyDictionary<string, string> ReadSpanAttributes(JsonElement span)
    {
        if (span.TryGetProperty("attributes", out var attributes))
        {
            return ObservabilityJson.ToOtelMap(attributes.Clone());
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }
}

