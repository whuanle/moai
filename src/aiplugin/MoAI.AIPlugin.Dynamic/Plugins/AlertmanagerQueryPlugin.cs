using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Alertmanager;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Alertmanager 查询（动态插件）：读取当前告警、静默与服务/集群状态，与 Prometheus 的 alerts/rules 互补补齐告警处置上下文.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="AlertmanagerQueryRequest.Mode"/>）：alerts / silences / status。只读由所用端点保证
/// （<c>/api/v2/alerts</c>、<c>/api/v2/silences</c>、<c>/api/v2/status</c> 均为读端点）；鉴权经
/// <see cref="OpsAuthorization"/>（Bearer 优先，或 Basic）。端点由插件按实例 BaseUrl（支持子路径）拼装，
/// 客户端为 <see cref="IAlertmanagerClient"/>（IHttpClientFactory 具名客户端，挂统一外部请求日志与遥测）。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件。
/// </remarks>
[AiPlugin(
    key: "alertmanager_query",
    Name = "Alertmanager 查询",
    Description = "查询 Alertmanager 告警处置上下文：Mode 支持 alerts（当前告警，含标签/状态/静默来源）/silences（静默列表，含匹配器）/status（版本与集群状态）；先以 {\"Mode\":\"alerts\"} 起步")]
public class AlertmanagerQueryPlugin : IDynamicPluginRuntime<AlertmanagerQueryRequest, AlertmanagerQueryResponse, AlertmanagerQueryConfig>
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
        "alerts", "silences", "status",
    };

    private readonly IAlertmanagerClient _client;
    private AlertmanagerQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertmanagerQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Alertmanager API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public AlertmanagerQueryPlugin(IAlertmanagerClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "alerts",       // alerts | silences | status
              "State": "active",      // alerts：状态过滤 active/suppressed/unprocessed（可空=全部）
              "SilencesState": "active" // silences：状态过滤 pending/active/expired（可空=全部）
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://alertmanager:9093", // Alertmanager 地址，支持子路径如 http://host/alertmanager
              "Username": "",          // Basic 用户名（可选）
              "Password": "",          // Basic 密码（可选）
              "BearerToken": "",       // Bearer Token（可选），已填时优先于用户名密码
              "TimeoutSeconds": 30,    // 单次请求超时秒数，1-300
              "MaxListItems": 100      // 列表最多返回条数，1-500
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(AlertmanagerQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new AlertmanagerQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Username = config.Username.Trim(),
            Password = config.Password,
            BearerToken = config.BearerToken.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxListItems = Math.Clamp(config.MaxListItems, MinMaxListItems, MaxMaxListItems),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<AlertmanagerQueryResponse> RunAsync(AlertmanagerQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, _config.BearerToken);
        return mode switch
        {
            "silences" => await SilencesAsync(request, authorization, cancellationToken).ConfigureAwait(false),
            "status" => await StatusAsync(authorization, cancellationToken).ConfigureAwait(false),
            _ => await AlertsAsync(request, authorization, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "alerts";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 alerts/silences/status");
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
            return "Alertmanager 服务地址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Alertmanager 服务地址 BaseUrl 必须是合法的 http:// 或 https:// URL";
        }

        return null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 把逗号分隔的状态过滤转成重复查询参数.
    /// </summary>
    private static List<KeyValuePair<string, string>> BuildStates(string? states, string key)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var state in (states ?? string.Empty).Split(','))
        {
            if (!string.IsNullOrWhiteSpace(state))
            {
                result.Add(new KeyValuePair<string, string>(key, state.Trim()));
            }
        }

        return result;
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
            throw new BusinessException(502, $"Alertmanager 请求超时（{_config.TimeoutSeconds}s），请检查服务地址与网络");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new BusinessException((int)ex.StatusCode.Value, $"Alertmanager 调用失败（HTTP {(int)ex.StatusCode.Value}）：{ex.Message}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Alertmanager 调用失败：{ex.Message}");
        }

        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "Alertmanager 响应不是合法 JSON，请检查 BaseUrl 是否指向 Alertmanager");
        }
    }

    private async Task<AlertmanagerQueryResponse> AlertsAsync(AlertmanagerQueryRequest request, string? authorization, CancellationToken cancellationToken)
    {
        var parameters = BuildStates(request.State, "state");
        var root = await SendAsync($"api/v2/alerts{BuildQuery(parameters)}", authorization, cancellationToken).ConfigureAwait(false);
        var alerts = new List<AlertmanagerAlert>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (alerts.Count >= _config.MaxListItems)
                {
                    break;
                }

                alerts.Add(new AlertmanagerAlert
                {
                    Labels = ObservabilityJson.ToStringMap(GetProperty(item, "labels")),
                    Annotations = ObservabilityJson.ToStringMap(GetProperty(item, "annotations")),
                    StartsAt = ObservabilityJson.GetStringProperty(item, "startsAt") ?? string.Empty,
                    EndsAt = ObservabilityJson.GetStringProperty(item, "endsAt") ?? string.Empty,
                    State = ObservabilityJson.GetStringProperty(item, "state") ?? string.Empty,
                    GeneratorUrl = ObservabilityJson.GetStringProperty(item, "generatorURL") ?? string.Empty,
                    SilencedBy = ReadStringArray(GetProperty(item, "silencedBy")),
                    InhibitedBy = ReadStringArray(GetProperty(item, "inhibitedBy")),
                });
            }
        }

        var truncated = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > _config.MaxListItems;
        return new AlertmanagerQueryResponse { ResultType = "alerts", Alerts = alerts, Truncated = truncated };
    }

    private async Task<AlertmanagerQueryResponse> SilencesAsync(AlertmanagerQueryRequest request, string? authorization, CancellationToken cancellationToken)
    {
        var parameters = BuildStates(request.SilencesState, "state");
        var root = await SendAsync($"api/v2/silences{BuildQuery(parameters)}", authorization, cancellationToken).ConfigureAwait(false);
        var silences = new List<AlertmanagerSilence>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (silences.Count >= _config.MaxListItems)
                {
                    break;
                }

                silences.Add(new AlertmanagerSilence
                {
                    Id = ObservabilityJson.GetStringProperty(item, "id") ?? string.Empty,
                    State = ObservabilityJson.GetStringProperty(GetProperty(item, "status"), "state") ?? string.Empty,
                    Matchers = ReadMatchers(GetProperty(item, "matchers")),
                    StartsAt = ObservabilityJson.GetStringProperty(item, "startsAt") ?? string.Empty,
                    EndsAt = ObservabilityJson.GetStringProperty(item, "endsAt") ?? string.Empty,
                    CreatedBy = ObservabilityJson.GetStringProperty(item, "createdBy") ?? string.Empty,
                    Comment = ObservabilityJson.GetStringProperty(item, "comment") ?? string.Empty,
                });
            }
        }

        var truncated = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > _config.MaxListItems;
        return new AlertmanagerQueryResponse { ResultType = "silences", Silences = silences, Truncated = truncated };
    }

    private async Task<AlertmanagerQueryResponse> StatusAsync(string? authorization, CancellationToken cancellationToken)
    {
        var root = await SendAsync("api/v2/status", authorization, cancellationToken).ConfigureAwait(false);
        var versionInfo = GetProperty(root, "versionInfo");
        var cluster = GetProperty(root, "cluster");
        var peers = new List<string>();
        if (cluster.ValueKind == JsonValueKind.Object && cluster.TryGetProperty("peers", out var peerArray) && peerArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var peer in peerArray.EnumerateArray())
            {
                var name = ObservabilityJson.GetStringProperty(peer, "name");
                if (!string.IsNullOrEmpty(name))
                {
                    peers.Add(name);
                }
            }
        }

        return new AlertmanagerQueryResponse
        {
            ResultType = "status",
            Status = new AlertmanagerStatus
            {
                Version = ObservabilityJson.GetStringProperty(versionInfo, "version") ?? string.Empty,
                ClusterStatus = ObservabilityJson.GetStringProperty(cluster, "status") ?? string.Empty,
                PeerNames = peers,
            },
        };
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    /// <summary>
    /// 匹配器归一为「名=值」（正则匹配器值带 ~ 前缀）.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadMatchers(JsonElement matchers)
    {
        var result = new Dictionary<string, string>();
        if (matchers.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var matcher in matchers.EnumerateArray())
        {
            var name = ObservabilityJson.GetStringProperty(matcher, "name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var value = ObservabilityJson.GetStringProperty(matcher, "value") ?? string.Empty;
            var isRegex = matcher.ValueKind == JsonValueKind.Object && matcher.TryGetProperty("isRegex", out var regex) && regex.ValueKind == JsonValueKind.True;
            result[name] = isRegex ? $"~{value}" : value;
        }

        return result;
    }

    private static List<string> ReadStringArray(JsonElement array)
    {
        var result = new List<string>();
        if (array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(item.GetString() ?? string.Empty);
            }
        }

        return result;
    }
}
