using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Elasticsearch;
using MoAI.Infra.Exceptions;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Elasticsearch 查询（动态插件）：用实例配置中的服务地址执行检索 DSL / 计数 / 字段映射 / 索引清单，供 AI 检索日志与文档.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="ElasticsearchQueryRequest.Mode"/>）：search / count / mappings / indices。
/// 只读由所用端点保证（_search/_count/_mapping/_cat 都是读端点），DSL 以 application/json 原文发送，
/// 解析层用 <see cref="ObservabilityJson"/> 容错展开；Refit 非 2xx 的 ApiException 会尝试从 ES 错误报文
/// <c>error.reason</c> 提取可读原因；_source/聚合按配置截断。每次运行由 <c>PluginExecutor</c> 创建独立
/// 作用域实例化插件；客户端为 transient，BaseAddress/超时按实例配置每次重设。
/// </remarks>
[AiPlugin(
    key: "elasticsearch_query",
    Name = "Elasticsearch 查询",
    Description = "对 Elasticsearch 执行查询表达式（DSL）：Mode 支持 search（DSL 检索+聚合）/count（计数）/mappings（字段映射）/indices（索引清单）；先以 mappings/indices 摸底字段，再以 {\"Mode\":\"search\",\"Dsl\":{\"query\":{\"match\":{\"message\":\"error\"}},\"size\":20}} 检索")]
public class ElasticsearchQueryPlugin : IDynamicPluginRuntime<ElasticsearchQueryRequest, ElasticsearchQueryResponse, ElasticsearchQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>命中条数上限的下界.</summary>
    private const int MinMaxHits = 1;

    /// <summary>命中条数上限的上界.</summary>
    private const int MaxMaxHits = 1000;

    /// <summary>单条命中 _source 字符数上限的下界.</summary>
    private const int MinSourceChars = 100;

    /// <summary>单条命中 _source 字符数上限的上界.</summary>
    private const int MaxSourceChars = 200000;

    /// <summary>聚合/映射字符数上限的下界.</summary>
    private const int MinResponseChars = 1;

    /// <summary>聚合/映射字符数上限的上界.</summary>
    private const int MaxResponseChars = 200000;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "search", "count", "mappings", "indices",
    };

    private readonly IElasticsearchClient _client;
    private ElasticsearchQueryConfig _config = new();
    private bool _prepared;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Elasticsearch API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public ElasticsearchQueryPlugin(IElasticsearchClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "search",                // search | count | mappings | indices
              "Index": "logs-*",               // 索引名：支持通配符与逗号分隔；留空 = 全部索引（_all）
              "Dsl": {                         // search/count 的查询 DSL（JSON 对象）
                "query": { "match": { "message": "error" } },
                "sort": [{ "@timestamp": "desc" }],
                "size": 20,
                "_source": ["level", "message", "service", "@timestamp"]
              }
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://es:9200", // Elasticsearch 服务地址，可含路径前缀
              "Username": "elastic",       // Basic 认证用户名（可选）；与 ApiKey 都未填则匿名
              "Password": "******",        // Basic 认证密码（可选）
              "ApiKey": "",                // ApiKey（可选）：Authorization: ApiKey 头，已填时优先
              "TimeoutSeconds": 30,        // 单次请求超时秒数，1-300
              "MaxHits": 100,              // search 最多返回命中条数，1-1000
              "MaxSourceCharsPerHit": 4000,// 单条命中 _source 最大字符数，100-200000
              "MaxResponseChars": 20000    // 聚合/映射字符数上限，1-200000
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(ElasticsearchQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new ElasticsearchQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Username = config.Username.Trim(),
            Password = config.Password,
            ApiKey = config.ApiKey.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxHits = Math.Clamp(config.MaxHits, MinMaxHits, MaxMaxHits),
            MaxSourceCharsPerHit = Math.Clamp(config.MaxSourceCharsPerHit, MinSourceChars, MaxSourceChars),
            MaxResponseChars = Math.Clamp(config.MaxResponseChars, MinResponseChars, MaxResponseChars),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<ElasticsearchQueryResponse> RunAsync(ElasticsearchQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode.Length == 0)
        {
            mode = "search";
        }

        if (!Modes.Contains(mode))
        {
            throw new BusinessException(400, $"不支持的 Mode={mode}：仅允许 search/count/mappings/indices");
        }

        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, null, _config.ApiKey);
        PrepareClient();
        var index = string.IsNullOrWhiteSpace(request.Index) ? "_all" : request.Index.Trim();

        try
        {
            return mode switch
            {
                "search" => ParseSearchResponse(await _client.SearchAsync(authorization, index, BuildBody(request.Dsl), cancellationToken).ConfigureAwait(false)),
                "count" => ParseCountResponse(await _client.CountAsync(authorization, index, BuildBody(request.Dsl), cancellationToken).ConfigureAwait(false)),
                "mappings" => ParseMappingResponse(await _client.MappingAsync(authorization, index, cancellationToken).ConfigureAwait(false)),
                _ => ParseIndicesResponse(await _client.CatIndicesAsync(authorization, "json", "health,status,index,uuid,pri,rep,docs.count,store.size", cancellationToken).ConfigureAwait(false)),
            };
        }
        catch (ApiException ex)
        {
            throw new BusinessException((int)ex.StatusCode, $"Elasticsearch 调用失败（HTTP {(int)ex.StatusCode}）：{ReadEsError(ex.Content, ex.ReasonPhrase)}");
        }
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

    /// <summary>
    /// 从 ES 错误报文里提取可读原因：优先 error.type 与 error.reason，缺失时回退原响应体.
    /// </summary>
    /// <param name="content">上游响应体.</param>
    /// <param name="reasonPhrase">HTTP 短语.</param>
    /// <returns>可读错误文本.</returns>
    private static string ReadEsError(string? content, string? reasonPhrase)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return reasonPhrase ?? string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var type = ObservabilityJson.GetStringProperty(error, "type");
                var reason = ObservabilityJson.GetStringProperty(error, "reason");
                if (!string.IsNullOrWhiteSpace(reason) || !string.IsNullOrWhiteSpace(type))
                {
                    return string.IsNullOrWhiteSpace(type) ? reason! : $"[{type}] {reason ?? "未知错误"}";
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON 报文：原样回传响应体
        }

        return content;
    }

    /// <summary>
    /// 组装请求体：无 DSL 或非对象形态时按空对象发送（ES search 可接受空体并按 match_all 检索）.
    /// </summary>
    /// <param name="dsl">请求里的 DSL.</param>
    /// <returns>可发送的 JSON 元素.</returns>
    private static JsonElement BuildBody(JsonElement dsl)
    {
        if (dsl.ValueKind == JsonValueKind.Object)
        {
            return dsl.Clone();
        }

        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private ElasticsearchQueryResponse ParseSearchResponse(string raw)
    {
        using var document = ObservabilityJson.ParseOrThrow(raw, "Elasticsearch");
        var root = document.RootElement;
        if (!root.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Object
            || !hits.TryGetProperty("hits", out var hitItems) || hitItems.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(502, "Elasticsearch 响应缺少 hits.hits 数组");
        }

        var resultHits = new List<ElasticsearchHit>();
        var anySourceTruncated = false;
        foreach (var hit in hitItems.EnumerateArray())
        {
            if (resultHits.Count >= _config.MaxHits)
            {
                break;
            }

            resultHits.Add(ReadHit(hit, ref anySourceTruncated));
        }

        var (aggJson, aggTruncated) = ReadAggregations(root);
        return new ElasticsearchQueryResponse
        {
            Mode = "search",
            TookMs = ReadLong(root, "took"),
            TotalHits = ReadTotalHits(hits),
            MaxScore = ObservabilityJson.GetStringProperty(hits, "max_score"),
            Hits = resultHits,
            AggregationsJson = aggJson,
            Truncated = resultHits.Count < hitItems.GetArrayLength() || anySourceTruncated || aggTruncated,
        };
    }

    private ElasticsearchHit ReadHit(JsonElement hit, ref bool anySourceTruncated)
    {
        var sourceJson = string.Empty;
        var sourceTruncated = false;
        if (hit.TryGetProperty("_source", out var source) && source.ValueKind == JsonValueKind.Object)
        {
            var raw = source.GetRawText();
            sourceJson = ObservabilityJson.Truncate(raw, _config.MaxSourceCharsPerHit);
            sourceTruncated = raw.Length > _config.MaxSourceCharsPerHit;
            anySourceTruncated = anySourceTruncated || sourceTruncated;
        }

        return new ElasticsearchHit
        {
            Index = ObservabilityJson.GetStringProperty(hit, "_index") ?? string.Empty,
            Id = ObservabilityJson.GetStringProperty(hit, "_id") ?? string.Empty,
            Score = ReadScore(hit),
            SourceJson = sourceJson,
            SourceTruncated = sourceTruncated,
        };
    }

    private static string? ReadScore(JsonElement hit)
    {
        if (!hit.TryGetProperty("_score", out var score) || score.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return score.ValueKind == JsonValueKind.Number ? score.GetRawText() : null;
    }

    private (string Json, bool Truncated) ReadAggregations(JsonElement root)
    {
        if (!root.TryGetProperty("aggregations", out var aggregations) || aggregations.ValueKind != JsonValueKind.Object)
        {
            return (string.Empty, false);
        }

        var raw = aggregations.GetRawText();
        return (ObservabilityJson.Truncate(raw, _config.MaxResponseChars), raw.Length > _config.MaxResponseChars);
    }

    private ElasticsearchQueryResponse ParseCountResponse(string raw)
    {
        using var document = ObservabilityJson.ParseOrThrow(raw, "Elasticsearch");
        var root = document.RootElement;
        return new ElasticsearchQueryResponse
        {
            Mode = "count",
            TotalHits = ReadLong(root, "count"),
            Count = ReadLong(root, "count"),
            TookMs = ReadLong(root, "took"),
        };
    }

    private ElasticsearchQueryResponse ParseMappingResponse(string raw)
    {
        using var check = ObservabilityJson.ParseOrThrow(raw, "Elasticsearch");
        return new ElasticsearchQueryResponse
        {
            Mode = "mappings",
            MappingJson = ObservabilityJson.Truncate(raw, _config.MaxResponseChars),
            Truncated = raw.Length > _config.MaxResponseChars,
        };
    }

    private ElasticsearchQueryResponse ParseIndicesResponse(string raw)
    {
        using var document = ObservabilityJson.ParseOrThrow(raw, "Elasticsearch");
        var root = document.RootElement;
        var rows = new List<ElasticsearchIndexRow>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in root.EnumerateArray())
            {
                rows.Add(new ElasticsearchIndexRow
                {
                    Health = ReadCatCell(row, "health"),
                    Status = ReadCatCell(row, "status"),
                    Index = ReadCatCell(row, "index"),
                    Pri = ReadCatCell(row, "pri"),
                    Rep = ReadCatCell(row, "rep"),
                    DocsCount = ReadCatCell(row, "docs.count"),
                    StoreSize = ReadCatCell(row, "store.size"),
                });
            }
        }

        return new ElasticsearchQueryResponse
        {
            Mode = "indices",
            Indices = rows,
            Truncated = false,
        };
    }

    private static string ReadCatCell(JsonElement row, string name)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => string.Empty,
        };
    }

    private static long ReadTotalHits(JsonElement hits)
    {
        if (!hits.TryGetProperty("total", out var totalValue))
        {
            return 0;
        }

        if (totalValue.ValueKind == JsonValueKind.Number && totalValue.TryGetInt64(out var number))
        {
            return number;
        }

        if (totalValue.ValueKind == JsonValueKind.Object)
        {
            return ReadLong(totalValue, "value");
        }

        return 0;
    }

    private static long ReadLong(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return 0;
    }
}

