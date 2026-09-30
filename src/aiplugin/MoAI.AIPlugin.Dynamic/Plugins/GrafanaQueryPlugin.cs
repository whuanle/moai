using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Grafana;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Grafana 查询（动态插件）：读取注解（事故时间线）、搜索仪表板与健康检查，补齐「事故发生了什么、何时、影响哪块看板」的上下文.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="GrafanaQueryRequest.Mode"/>）：health / annotations / search。全程只读端点
/// （<c>/api/health</c>、<c>/api/annotations</c>、<c>/api/search</c>）。鉴权经 <see cref="OpsAuthorization"/>：
/// 服务账号 Token → Bearer，或 Basic 用户名密码。端点由插件按实例配置拼装（BaseAddress 支持子路径部署），
/// 客户端为 <see cref="IGrafanaClient"/>（IHttpClientFactory 具名客户端，挂统一外部请求日志与遥测）。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件。
/// </remarks>
[AiPlugin(
    key: "grafana_query",
    Name = "Grafana 查询",
    Description = "查询 Grafana：Mode 支持 health（连通性与版本）/annotations（注解=事故时间线，支持时间范围与标签过滤）/search（仪表板搜索）；先以 {\"Mode\":\"annotations\",\"Tags\":\"alerting\",\"From\":\"2026-09-29T00:00:00Z\"} 起步")]
public class GrafanaQueryPlugin : IDynamicPluginRuntime<GrafanaQueryRequest, GrafanaQueryResponse, GrafanaQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>列表条数上限的下界.</summary>
    private const int MinMaxListItems = 1;

    /// <summary>列表条数上限的上界.</summary>
    private const int MaxMaxListItems = 500;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "health", "annotations", "search",
    };

    private readonly IGrafanaClient _client;
    private GrafanaQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GrafanaQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Grafana API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public GrafanaQueryPlugin(IGrafanaClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "annotations",   // health | annotations | search
              "From": "2026-09-29T00:00:00Z", // annotations：起始时间（RFC3339 或毫秒时间戳），可空
              "To": null,              // annotations：结束时间，可空=不限
              "Tags": "alerting,deploy", // annotations：标签过滤（逗号分隔），可空
              "Query": "nginx"         // search：仪表板搜索关键字，可空=全部
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://grafana.example.com", // Grafana 地址，支持子路径如 http://host/grafana
              "Token": "",             // 服务账号令牌（glsa_...），已填时优先于用户名密码
              "Username": "",          // Basic 用户名（可选）
              "Password": "",          // Basic 密码（可选）
              "TimeoutSeconds": 30,    // 单次请求超时秒数，1-300
              "MaxListItems": 100      // 注解/仪表板列表最多返回条数，1-500
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(GrafanaQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new GrafanaQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Token = config.Token.Trim(),
            Username = config.Username.Trim(),
            Password = config.Password,
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxListItems = Math.Clamp(config.MaxListItems, MinMaxListItems, MaxMaxListItems),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<GrafanaQueryResponse> RunAsync(GrafanaQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, _config.Token);
        return mode switch
        {
            "annotations" => await AnnotationsAsync(request, authorization, cancellationToken).ConfigureAwait(false),
            "search" => await SearchAsync(request, authorization, cancellationToken).ConfigureAwait(false),
            _ => await HealthAsync(authorization, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "health";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 health/annotations/search");
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
            return "Grafana 地址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Grafana 地址 BaseUrl 必须是合法的 http:// 或 https:// URL";
        }

        return null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 归一时间参数：RFC3339 或毫秒时间戳原文均可.
    /// </summary>
    private static string? NormalizeEpochMs(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(trimmed, out var time))
        {
            return time.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return long.TryParse(trimmed, out _) ? trimmed : null;
    }

    /// <summary>
    /// 拼查询串（键值均转义；同键多值重复下发）.
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
            throw new BusinessException(502, $"Grafana 请求超时（{_config.TimeoutSeconds}s），请检查服务地址与网络");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new BusinessException((int)ex.StatusCode.Value, $"Grafana 调用失败（HTTP {(int)ex.StatusCode.Value}）：{ex.Message}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Grafana 调用失败：{ex.Message}");
        }

        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "Grafana 响应不是合法 JSON，请检查 BaseUrl 是否指向 Grafana");
        }
    }

    private async Task<GrafanaQueryResponse> HealthAsync(string? authorization, CancellationToken cancellationToken)
    {
        var health = await SendAsync("api/health", authorization, cancellationToken).ConfigureAwait(false);
        return new GrafanaQueryResponse { ResultType = "health", Health = GrafanaResponseParser.ParseHealth(health) };
    }

    private async Task<GrafanaQueryResponse> AnnotationsAsync(GrafanaQueryRequest request, string? authorization, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("limit", _config.MaxListItems.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        var from = NormalizeEpochMs(request.From);
        var to = NormalizeEpochMs(request.To);
        if (from != null)
        {
            parameters.Add(new KeyValuePair<string, string>("from", from));
        }

        if (to != null)
        {
            parameters.Add(new KeyValuePair<string, string>("to", to));
        }

        foreach (var tag in (request.Tags ?? string.Empty).Split(','))
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                parameters.Add(new KeyValuePair<string, string>("tags", tag.Trim()));
            }
        }

        var root = await SendAsync($"api/annotations{BuildQuery(parameters)}", authorization, cancellationToken).ConfigureAwait(false);
        var truncated = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > _config.MaxListItems;
        var annotations = GrafanaResponseParser.ParseAnnotations(root);
        if (annotations.Count > _config.MaxListItems)
        {
            annotations = annotations.GetRange(0, _config.MaxListItems);
        }

        return new GrafanaQueryResponse { ResultType = "annotations", Annotations = annotations, Truncated = truncated };
    }

    private async Task<GrafanaQueryResponse> SearchAsync(GrafanaQueryRequest request, string? authorization, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("type", "dash-db"),
            new("limit", _config.MaxListItems.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        var query = request.Query?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            parameters.Add(new KeyValuePair<string, string>("query", query));
        }

        var root = await SendAsync($"api/search{BuildQuery(parameters)}", authorization, cancellationToken).ConfigureAwait(false);
        var truncated = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > _config.MaxListItems;
        var dashboards = GrafanaResponseParser.ParseDashboards(root);
        if (dashboards.Count > _config.MaxListItems)
        {
            dashboards = dashboards.GetRange(0, _config.MaxListItems);
        }

        return new GrafanaQueryResponse { ResultType = "search", Dashboards = dashboards, Truncated = truncated };
    }
}
