using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Kubernetes;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Kubernetes 查询（动态插件）：只读读取 Pod/事件/Deployment/节点与 Pod 日志，为智能运维补上集群定位腿.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="KubernetesQueryRequest.Mode"/>）：pods / pod_logs / events / deployments / nodes。
/// 全程只读 GET；鉴权为 Bearer 令牌（建议只读 ServiceAccount）；API Server 证书多为私有 CA 签发，
/// <c>SkipTlsVerify</c> 按请求生效（见 <see cref="IKubernetesClient"/>）。列表 <c>limit</c> 服务端截断 +
/// 客户端再截断（不翻页，超出 Truncated 置真）。每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件.
/// </remarks>
[AiPlugin(
    key: "kubernetes_query",
    Name = "Kubernetes 查询",
    Description = "只读查询 Kubernetes：Mode 支持 pods（Pod 列表）/pod_logs（Pod 日志）/events（事件）/deployments（Deployment）/nodes（节点）；Bearer 令牌鉴权；先以 {\"Mode\":\"pods\"} 起步，异常 Pod 再取 pod_logs")]
public class KubernetesQueryPlugin : IDynamicPluginRuntime<KubernetesQueryRequest, KubernetesQueryResponse, KubernetesQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>日志尾部行数的上界.</summary>
    private const int MaxTailLines = 1000;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "pods", "pod_logs", "events", "deployments", "nodes",
    };

    private readonly IKubernetesClient _client;
    private KubernetesQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KubernetesQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Kubernetes API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public KubernetesQueryPlugin(IKubernetesClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "pods",            // pods | pod_logs | events | deployments | nodes
              "Namespace": "prod",       // 命名空间（可空=全部）
              "LabelSelector": "app=nginx", // pods：labelSelector
              "Pod": "nginx-7d9f-x1",    // pod_logs：Pod 名（必填）
              "Container": "nginx",      // pod_logs：容器名（多容器必填）
              "TailLines": 200,          // pod_logs：尾部行数
              "Previous": false          // pod_logs：是否取上一次崩溃容器日志
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "https://192.168.1.10:6443", // Kubernetes API Server 地址
              "Token": "eyJhbGciOi...",   // Bearer 令牌（建议只读 ServiceAccount）
              "SkipTlsVerify": true,      // 跳过 TLS 校验（自签 CA 的 API Server 保持 true）
              "TimeoutSeconds": 30,       // 单次请求超时秒数，1-300
              "MaxListItems": 100,        // 列表最多返回条数，1-500
              "MaxLogChars": 16384        // Pod 日志最大返回字符数，1024-131072
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(KubernetesQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new KubernetesQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Token = config.Token.Trim(),
            SkipTlsVerify = config.SkipTlsVerify,
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxListItems = Math.Clamp(config.MaxListItems, 1, 500),
            MaxLogChars = Math.Clamp(config.MaxLogChars, 1024, 131072),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<KubernetesQueryResponse> RunAsync(KubernetesQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request);
        return mode switch
        {
            "pod_logs" => await PodLogsAsync(request, cancellationToken).ConfigureAwait(false),
            "events" => await EventsAsync(request, cancellationToken).ConfigureAwait(false),
            "deployments" => await DeploymentsAsync(request, cancellationToken).ConfigureAwait(false),
            "nodes" => await NodesAsync(cancellationToken).ConfigureAwait(false),
            _ => await PodsAsync(request, cancellationToken).ConfigureAwait(false),
        };
    }

    private string NormalizeMode(KubernetesQueryRequest request)
    {
        var normalized = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "pods";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 pods/pod_logs/events/deployments/nodes");
        }

        if (normalized == "pod_logs")
        {
            if (string.IsNullOrWhiteSpace(request.Namespace))
            {
                throw new BusinessException(400, "pod_logs 模式需要提供命名空间 Namespace");
            }

            if (string.IsNullOrWhiteSpace(request.Pod))
            {
                throw new BusinessException(400, "pod_logs 模式需要提供 Pod 名 Pod");
            }
        }

        return normalized;
    }

    /// <summary>
    /// 校验 BaseUrl.
    /// </summary>
    /// <param name="baseUrl">配置里的 API Server 地址.</param>
    /// <returns>校验失败信息；通过返回 null.</returns>
    private static string? ValidateBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "Kubernetes API Server 地址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Kubernetes API Server 地址 BaseUrl 必须是合法的 http:// 或 https:// URL";
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

    private async Task<JsonElement> SendListAsync(string relativePathWithQuery, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(_config.BaseUrl), relativePathWithQuery);
        string raw;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            raw = await _client.GetAsync(endpoint, _config.Token.Length > 0 ? _config.Token : null, _config.SkipTlsVerify, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(502, $"Kubernetes 请求超时（{_config.TimeoutSeconds}s），请检查 API Server 地址与网络");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            var hint = (int)ex.StatusCode.Value switch
            {
                401 => "令牌无效或缺失，请检查实例配置的 Token",
                403 => "令牌权限不足（建议绑定只读 ClusterRole 的 ServiceAccount）",
                _ => ex.Message,
            };
            throw new BusinessException((int)ex.StatusCode.Value, $"Kubernetes 调用失败（HTTP {(int)ex.StatusCode.Value}）：{hint}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Kubernetes 调用失败：{ex.Message}");
        }

        try
        {
            var root = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw).RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "Status")
            {
                // API 返回 Status 对象即错误（如 404 资源不存在）
                var message = ObservabilityJson.GetStringProperty(root, "message") ?? "未知错误";
                var code = root.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsed) ? parsed : 500;
                throw new BusinessException(502, $"Kubernetes 调用失败：{message}");
            }

            return root.Clone();
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "Kubernetes 响应不是合法 JSON，请检查 BaseUrl 是否指向 API Server");
        }
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    private static JsonElement GetArrayOrEmpty(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Array ? value : default;
    }

    /// <summary>
    /// K8s 列表响应取 items 数组.
    /// </summary>
    private static JsonElement GetItems(JsonElement root)
    {
        return GetArrayOrEmpty(root, "items");
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

    private async Task<KubernetesQueryResponse> PodsAsync(KubernetesQueryRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("limit", _config.MaxListItems.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        if (!string.IsNullOrWhiteSpace(request.LabelSelector))
        {
            parameters.Add(new KeyValuePair<string, string>("labelSelector", request.LabelSelector.Trim()));
        }

        var path = string.IsNullOrWhiteSpace(request.Namespace)
            ? $"api/v1/pods{BuildQuery(parameters)}"
            : $"api/v1/namespaces/{Uri.EscapeDataString(request.Namespace.Trim())}/pods{BuildQuery(parameters)}";
        var root = await SendListAsync(path, cancellationToken).ConfigureAwait(false);
        var pods = new List<KubernetesPod>();
        foreach (var item in GetItems(root).EnumerateArray())
        {
            if (pods.Count >= _config.MaxListItems)
            {
                break;
            }

            var metadata = GetProperty(item, "metadata");
            var status = GetProperty(item, "status");
            var containersReady = 0;
            var containersTotal = 0;
            var restarts = 0;
            if (status.TryGetProperty("containerStatuses", out var containerStatuses) && containerStatuses.ValueKind == JsonValueKind.Array)
            {
                foreach (var container in containerStatuses.EnumerateArray())
                {
                    containersTotal++;
                    if (container.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True)
                    {
                        containersReady++;
                    }

                    if (container.TryGetProperty("restartCount", out var restartCount) && restartCount.TryGetInt32(out var restart))
                    {
                        restarts += restart;
                    }
                }
            }

            pods.Add(new KubernetesPod
            {
                Name = ObservabilityJson.GetStringProperty(metadata, "name") ?? string.Empty,
                Namespace = ObservabilityJson.GetStringProperty(metadata, "namespace") ?? string.Empty,
                Phase = ObservabilityJson.GetStringProperty(status, "phase") ?? string.Empty,
                PodIp = ObservabilityJson.GetStringProperty(status, "podIP") ?? string.Empty,
                NodeName = ObservabilityJson.GetStringProperty(status, "nodeName") ?? string.Empty,
                ReadyContainers = $"{containersReady}/{containersTotal}",
                RestartCount = restarts,
                CreatedAt = ObservabilityJson.GetStringProperty(metadata, "creationTimestamp") ?? string.Empty,
                Labels = ObservabilityJson.ToStringMap(GetProperty(metadata, "labels")),
            });
        }

        var truncated = GetItems(root).ValueKind == JsonValueKind.Array && GetItems(root).GetArrayLength() > _config.MaxListItems;
        return new KubernetesQueryResponse { ResultType = "pods", Pods = pods, Truncated = truncated };
    }

    private async Task<KubernetesQueryResponse> PodLogsAsync(KubernetesQueryRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("tailLines", Math.Clamp(request.TailLines <= 0 ? 200 : request.TailLines, 1, MaxTailLines).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        if (!string.IsNullOrWhiteSpace(request.Container))
        {
            parameters.Add(new KeyValuePair<string, string>("container", request.Container.Trim()));
        }

        if (request.Previous)
        {
            parameters.Add(new KeyValuePair<string, string>("previous", "true"));
        }

        var path = $"api/v1/namespaces/{Uri.EscapeDataString(request.Namespace!.Trim())}/pods/{Uri.EscapeDataString(request.Pod!.Trim())}/log{BuildQuery(parameters)}";
        var endpoint = new Uri(new Uri(_config.BaseUrl), path);
        string raw;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            raw = await _client.GetAsync(endpoint, _config.Token.Length > 0 ? _config.Token : null, _config.SkipTlsVerify, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(502, $"Kubernetes 请求超时（{_config.TimeoutSeconds}s）");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new BusinessException((int)ex.StatusCode.Value, $"Kubernetes 调用失败（HTTP {(int)ex.StatusCode.Value}）：{ex.Message}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Kubernetes 调用失败：{ex.Message}");
        }

        return new KubernetesQueryResponse
        {
            ResultType = "pod_logs",
            LogText = ObservabilityJson.Truncate(raw ?? string.Empty, _config.MaxLogChars),
            Container = request.Container?.Trim(),
        };
    }

    private async Task<KubernetesQueryResponse> EventsAsync(KubernetesQueryRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("limit", _config.MaxListItems.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        var path = string.IsNullOrWhiteSpace(request.Namespace)
            ? $"api/v1/events{BuildQuery(parameters)}"
            : $"api/v1/namespaces/{Uri.EscapeDataString(request.Namespace.Trim())}/events{BuildQuery(parameters)}";
        var root = await SendListAsync(path, cancellationToken).ConfigureAwait(false);
        var events = new List<KubernetesEvent>();
        foreach (var item in GetItems(root).EnumerateArray())
        {
            if (events.Count >= _config.MaxListItems)
            {
                break;
            }

            var involved = GetProperty(item, "involvedObject");
            events.Add(new KubernetesEvent
            {
                Type = ObservabilityJson.GetStringProperty(item, "type") ?? string.Empty,
                Reason = ObservabilityJson.GetStringProperty(item, "reason") ?? string.Empty,
                Message = ObservabilityJson.GetStringProperty(item, "message") ?? string.Empty,
                ObjectKind = ObservabilityJson.GetStringProperty(involved, "kind") ?? string.Empty,
                ObjectName = ObservabilityJson.GetStringProperty(involved, "name") ?? string.Empty,
                ObjectNamespace = ObservabilityJson.GetStringProperty(involved, "namespace") ?? string.Empty,
                LastTimestamp = ObservabilityJson.GetStringProperty(item, "lastTimestamp") ?? string.Empty,
                Count = item.TryGetProperty("count", out var count) && count.TryGetInt32(out var parsed) ? parsed : 0,
            });
        }

        var truncated = GetItems(root).ValueKind == JsonValueKind.Array && GetItems(root).GetArrayLength() > _config.MaxListItems;
        return new KubernetesQueryResponse { ResultType = "events", Events = events, Truncated = truncated };
    }

    private async Task<KubernetesQueryResponse> DeploymentsAsync(KubernetesQueryRequest request, CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("limit", _config.MaxListItems.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        var path = string.IsNullOrWhiteSpace(request.Namespace)
            ? $"apis/apps/v1/deployments{BuildQuery(parameters)}"
            : $"apis/apps/v1/namespaces/{Uri.EscapeDataString(request.Namespace.Trim())}/deployments{BuildQuery(parameters)}";
        var root = await SendListAsync(path, cancellationToken).ConfigureAwait(false);
        var deployments = new List<KubernetesDeployment>();
        foreach (var item in GetItems(root).EnumerateArray())
        {
            if (deployments.Count >= _config.MaxListItems)
            {
                break;
            }

            var metadata = GetProperty(item, "metadata");
            var spec = GetProperty(item, "spec");
            var status = GetProperty(item, "status");
            var images = new List<string>();
            if (spec.TryGetProperty("template", out var template) && template.TryGetProperty("spec", out var podSpec) && podSpec.TryGetProperty("containers", out var containers) && containers.ValueKind == JsonValueKind.Array)
            {
                foreach (var container in containers.EnumerateArray())
                {
                    var image = ObservabilityJson.GetStringProperty(container, "image");
                    if (!string.IsNullOrEmpty(image))
                    {
                        images.Add(image);
                    }
                }
            }

            deployments.Add(new KubernetesDeployment
            {
                Name = ObservabilityJson.GetStringProperty(metadata, "name") ?? string.Empty,
                Namespace = ObservabilityJson.GetStringProperty(metadata, "namespace") ?? string.Empty,
                Replicas = spec.TryGetProperty("replicas", out var replicas) && replicas.TryGetInt32(out var desired) ? desired : 0,
                ReadyReplicas = status.TryGetProperty("readyReplicas", out var readyReplicas) && readyReplicas.TryGetInt32(out var ready) ? ready : 0,
                AvailableReplicas = status.TryGetProperty("availableReplicas", out var availableReplicas) && availableReplicas.TryGetInt32(out var available) ? available : 0,
                Images = images,
                CreatedAt = ObservabilityJson.GetStringProperty(metadata, "creationTimestamp") ?? string.Empty,
            });
        }

        var truncated = GetItems(root).ValueKind == JsonValueKind.Array && GetItems(root).GetArrayLength() > _config.MaxListItems;
        return new KubernetesQueryResponse { ResultType = "deployments", Deployments = deployments, Truncated = truncated };
    }

    private async Task<KubernetesQueryResponse> NodesAsync(CancellationToken cancellationToken)
    {
        var root = await SendListAsync($"api/v1/nodes{BuildQuery(new List<KeyValuePair<string, string>>())}", cancellationToken).ConfigureAwait(false);
        var nodes = new List<KubernetesNode>();
        foreach (var item in GetItems(root).EnumerateArray())
        {
            if (nodes.Count >= _config.MaxListItems)
            {
                break;
            }

            var metadata = GetProperty(item, "metadata");
            var status = GetProperty(item, "status");
            var ready = false;
            if (status.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
            {
                foreach (var condition in conditions.EnumerateArray())
                {
                    if (string.Equals(ObservabilityJson.GetStringProperty(condition, "type"), "Ready", StringComparison.Ordinal)
                        && condition.TryGetProperty("status", out var conditionStatus)
                        && string.Equals(conditionStatus.GetString(), "True", StringComparison.Ordinal))
                    {
                        ready = true;
                        break;
                    }
                }
            }

            var internalIp = string.Empty;
            if (status.TryGetProperty("addresses", out var addresses) && addresses.ValueKind == JsonValueKind.Array)
            {
                foreach (var address in addresses.EnumerateArray())
                {
                    if (string.Equals(ObservabilityJson.GetStringProperty(address, "type"), "InternalIP", StringComparison.Ordinal))
                    {
                        internalIp = ObservabilityJson.GetStringProperty(address, "address") ?? string.Empty;
                        break;
                    }
                }
            }

            var allocatable = GetProperty(status, "allocatable");
            nodes.Add(new KubernetesNode
            {
                Name = ObservabilityJson.GetStringProperty(metadata, "name") ?? string.Empty,
                Ready = ready,
                KubeletVersion = ObservabilityJson.GetStringProperty(GetProperty(status, "nodeInfo"), "kubeletVersion") ?? string.Empty,
                Cpu = ObservabilityJson.GetStringProperty(allocatable, "cpu") ?? string.Empty,
                Memory = ObservabilityJson.GetStringProperty(allocatable, "memory") ?? string.Empty,
                InternalIp = internalIp,
            });
        }

        var truncated = GetItems(root).ValueKind == JsonValueKind.Array && GetItems(root).GetArrayLength() > _config.MaxListItems;
        return new KubernetesQueryResponse { ResultType = "nodes", Nodes = nodes, Truncated = truncated };
    }
}
