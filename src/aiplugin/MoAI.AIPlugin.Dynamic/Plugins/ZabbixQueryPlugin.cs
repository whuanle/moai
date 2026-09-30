using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Zabbix;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// Zabbix 查询（动态插件）：让 AI 读取 Zabbix 当前问题/主机/触发器做告警巡检与资产定位.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="ZabbixQueryRequest.Mode"/>）：version / problems / hosts / triggers。
/// 单端点 JSON-RPC（<c>api_jsonrpc.php</c>，BaseUrl 支持子路径部署），凭证支持 API 令牌或用户名密码
/// （user.login 换会话，兼容 username/user 两种参数名），凭证形态由 UseHeaderAuth 切换
/// （6.4+ 的 Authorization 头或 5.x-7.x 通用的 body auth 属性）。全程只读方法，problems 结果
/// 用 trigger.get 二次查询富化主机名；列表超过 MaxListItems 截断并置 Truncated。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件。
/// </remarks>
[AiPlugin(
    key: "zabbix_query",
    Name = "Zabbix 查询",
    Description = "查询 Zabbix 告警与资产：Mode 支持 version（服务端版本）/problems（当前问题，默认）/hosts（主机）/triggers（问题态触发器）；支持 API 令牌或用户名密码鉴权；先以 {\"Mode\":\"problems\",\"SeverityMin\":2} 起步看警告以上问题")]
public class ZabbixQueryPlugin : IDynamicPluginRuntime<ZabbixQueryRequest, ZabbixQueryResponse, ZabbixQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>列表条数上限的下界.</summary>
    private const int MinMaxListItems = 1;

    /// <summary>列表条数上限的上界.</summary>
    private const int MaxMaxListItems = 500;

    /// <summary>严重级的上界.</summary>
    private const int MaxSeverity = 5;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "version", "problems", "hosts", "triggers",
    };

    private readonly IZabbixClient _client;
    private ZabbixQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ZabbixQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">Zabbix JSON-RPC 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public ZabbixQueryPlugin(IZabbixClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "problems",     // version | problems | hosts | triggers
              "SeverityMin": 2,       // 最低严重级 0=未分类 1=信息 2=警告 3=一般 4=严重 5=灾难
              "SearchHost": "web-01", // 主机名模糊过滤（hosts/triggers 模式）
              "HostGroupIds": "2,5"   // 主机群组 ID 过滤（逗号分隔，hosts/triggers 模式）
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://zabbix.example.com", // Zabbix 前端地址，支持子路径如 http://host/zabbix
              "Token": "",             // API 令牌（可选，Zabbix 5.4+），已填时优先于用户名密码
              "Username": "",          // 用户名（可选，与密码配合走 user.login）
              "Password": "",          // 密码（可选）
              "UseHeaderAuth": false,  // true=凭证放 Authorization: Bearer 头（6.4+ 推荐）；false=放 JSON-RPC auth 属性（兼容 5.x-7.x）
              "TimeoutSeconds": 30,    // 单次请求超时秒数，1-300
              "MaxListItems": 100      // 列表最多返回条数，1-500
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(ZabbixQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new ZabbixQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Token = config.Token.Trim(),
            Username = config.Username.Trim(),
            Password = config.Password,
            UseHeaderAuth = config.UseHeaderAuth,
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxListItems = Math.Clamp(config.MaxListItems, MinMaxListItems, MaxMaxListItems),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<ZabbixQueryResponse> RunAsync(ZabbixQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        if (mode == "version")
        {
            return await VersionAsync(cancellationToken).ConfigureAwait(false);
        }

        var credential = await EnsureCredentialAsync(cancellationToken).ConfigureAwait(false);
        return mode switch
        {
            "hosts" => await HostsAsync(request, credential, cancellationToken).ConfigureAwait(false),
            "triggers" => await TriggersAsync(request, credential, cancellationToken).ConfigureAwait(false),
            _ => await ProblemsAsync(request, credential, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "problems";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 version/problems/hosts/triggers");
        }

        return normalized;
    }

    /// <summary>
    /// 校验 BaseUrl.
    /// </summary>
    /// <param name="baseUrl">配置里的前端地址.</param>
    /// <returns>校验失败信息；通过返回 null.</returns>
    private static string? ValidateBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "Zabbix 前端地址 BaseUrl 不能为空";
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Zabbix 前端地址 BaseUrl 必须是合法的 http:// 或 https:// URL";
        }

        return null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    /// <summary>
    /// 解析逗号分隔的群组 ID 为数组.
    /// </summary>
    private static long[] ParseGroupIds(string? hostGroupIds)
    {
        if (string.IsNullOrWhiteSpace(hostGroupIds))
        {
            return Array.Empty<long>();
        }

        var ids = new List<long>();
        foreach (var part in hostGroupIds.Split(','))
        {
            if (!long.TryParse(part.Trim(), out var id) || id <= 0)
            {
                throw new BusinessException(400, $"HostGroupIds 需为逗号分隔的正整数 ID，收到「{part.Trim()}」");
            }

            ids.Add(id);
        }

        return ids.ToArray();
    }

    /// <summary>
    /// 宽松读取整数字段（Zabbix 部分字段以字符串形式返回数字）.
    /// </summary>
    private static int GetIntProperty(JsonElement parent, string name, int fallback = 0)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : fallback,
            JsonValueKind.String => int.TryParse(value.GetString(), out var parsed) ? parsed : fallback,
            _ => fallback,
        };
    }

    /// <summary>
    /// 取得本次运行的鉴权凭证：API 令牌优先，否则 user.login 换会话；都未配置则拒绝.
    /// </summary>
    private async Task<string> EnsureCredentialAsync(CancellationToken cancellationToken)
    {
        if (_config.Token.Length > 0)
        {
            return _config.Token;
        }

        if (_config.Username.Length > 0)
        {
            return await LoginAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new BusinessException(400, "需要在实例配置中提供 API 令牌 Token 或用户名密码 Username/Password");
    }

    /// <summary>
    /// user.login 换取会话：优先 username 参数名（5.4+/6.x/7.x），失败回退 user（5.0 及更早）.
    /// </summary>
    private async Task<string> LoginAsync(CancellationToken cancellationToken)
    {
        string? firstError;
        try
        {
            return RequireString(await SendRpcAsync(
                "user.login",
                new Dictionary<string, object?> { ["username"] = _config.Username, ["password"] = _config.Password },
                null,
                cancellationToken).ConfigureAwait(false), "user.login");
        }
        catch (BusinessException ex)
        {
            firstError = ex.Message;
        }

        try
        {
            return RequireString(await SendRpcAsync(
                "user.login",
                new Dictionary<string, object?> { ["user"] = _config.Username, ["password"] = _config.Password },
                null,
                cancellationToken).ConfigureAwait(false), "user.login");
        }
        catch (BusinessException)
        {
            throw new BusinessException(401, $"Zabbix 登录失败：{firstError}");
        }
    }

    private static string RequireString(JsonElement result, string method)
    {
        var value = result.ValueKind == JsonValueKind.String ? result.GetString() : null;
        if (string.IsNullOrEmpty(value))
        {
            throw new BusinessException(502, $"Zabbix {method} 未返回预期结果");
        }

        return value!;
    }

    /// <summary>
    /// 发送一条 JSON-RPC 并解析 result；error 响应归一为业务失败.
    /// </summary>
    private async Task<JsonElement> SendRpcAsync(string method, Dictionary<string, object?> parameters, string? credential, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(_config.BaseUrl), "api_jsonrpc.php");
        var body = new ZabbixRpcRequest
        {
            Method = method,
            Params = parameters,
            Auth = _config.UseHeaderAuth ? null : credential,
        };
        var authorization = _config.UseHeaderAuth && credential != null ? $"Bearer {credential}" : null;

        string raw;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            raw = await _client.RpcAsync(endpoint, authorization, body, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(502, $"Zabbix 请求超时（{_config.TimeoutSeconds}s），请检查前端地址与网络");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new BusinessException((int)ex.StatusCode.Value, $"Zabbix 调用失败（HTTP {(int)ex.StatusCode.Value}）：{ex.Message}");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new BusinessException(502, $"Zabbix 调用失败：{ex.Message}");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch (JsonException)
        {
            throw new BusinessException(502, "Zabbix 响应不是合法 JSON，请检查 BaseUrl 是否指向 Zabbix 前端");
        }

        using (document)
        {
            var error = ZabbixResponseParser.ExtractError(document.RootElement);
            if (error != null)
            {
                throw new BusinessException(502, $"Zabbix 调用失败：{error}");
            }

            if (!document.RootElement.TryGetProperty("result", out var result))
            {
                throw new BusinessException(502, "Zabbix 响应缺少 result 字段");
            }

            return result.Clone();
        }
    }

    private async Task<ZabbixQueryResponse> VersionAsync(CancellationToken cancellationToken)
    {
        var result = await SendRpcAsync("apiinfo.version", new Dictionary<string, object?>(), null, cancellationToken).ConfigureAwait(false);
        return new ZabbixQueryResponse { ResultType = "version", Version = result.ValueKind == JsonValueKind.String ? result.GetString() : string.Empty };
    }

    private async Task<ZabbixQueryResponse> ProblemsAsync(ZabbixQueryRequest request, string credential, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["output"] = "extend",
            ["selectTags"] = "extend",
            ["sortfield"] = "eventid",
            ["sortorder"] = "DESC",
            ["limit"] = _config.MaxListItems + 1,
        };
        if (request.SeverityMin > 0)
        {
            var min = Math.Clamp(request.SeverityMin, 1, MaxSeverity);
            parameters["severities"] = Enumerable.Range(min, MaxSeverity - min + 1).ToArray();
        }

        var problems = ReadArray(await SendRpcAsync("problem.get", parameters, credential, cancellationToken).ConfigureAwait(false), "problem.get");
        var truncated = problems.Count > _config.MaxListItems;
        if (truncated)
        {
            problems = problems.GetRange(0, _config.MaxListItems);
        }

        var parsedWithTrigger = problems
            .Select(item => (Problem: ParseProblem(item), TriggerId: ObservabilityJson.GetStringProperty(item, "objectid") ?? string.Empty))
            .ToList();
        await FillHostNamesAsync(parsedWithTrigger, credential, cancellationToken).ConfigureAwait(false);
        return new ZabbixQueryResponse { ResultType = "problems", Problems = parsedWithTrigger.Select(p => p.Problem).ToList(), Truncated = truncated };
    }

    private static ZabbixProblem ParseProblem(JsonElement item)
    {
        var clock = ObservabilityJson.GetStringProperty(item, "clock");
        return new ZabbixProblem
        {
            EventId = ObservabilityJson.GetStringProperty(item, "eventid") ?? string.Empty,
            Severity = GetIntProperty(item, "severity"),
            SeverityName = ZabbixResponseParser.SeverityName(GetIntProperty(item, "severity")),
            Name = ObservabilityJson.GetStringProperty(item, "name") ?? string.Empty,
            Clock = ZabbixResponseParser.ClockToIso(clock),
            Age = ZabbixResponseParser.AgeFromClock(clock),
            Acknowledged = GetIntProperty(item, "acknowledgement") == 1,
            Tags = ZabbixResponseParser.ParseTags(GetProperty(item, "tags")),
        };
    }

    /// <summary>
    /// problem 对象不含主机名，用 trigger.get（triggerids=问题的 objectid）二次查询富化.
    /// </summary>
    private async Task FillHostNamesAsync(List<(ZabbixProblem Problem, string TriggerId)> problems, string credential, CancellationToken cancellationToken)
    {
        var triggerIds = problems
            .Select(p => p.TriggerId)
            .Where(id => id.Length > 0)
            .Distinct()
            .ToList();
        if (triggerIds.Count == 0)
        {
            return;
        }

        var triggers = ReadArray(await SendRpcAsync(
            "trigger.get",
            new Dictionary<string, object?>
            {
                ["triggerids"] = triggerIds,
                ["output"] = new[] { "triggerid" },
                ["selectHosts"] = new[] { "hostid", "host", "name" },
            },
            credential,
            cancellationToken).ConfigureAwait(false), "trigger.get");

        var hostByTrigger = new Dictionary<string, (string HostId, string Name)>(StringComparer.Ordinal);
        foreach (var trigger in triggers)
        {
            var triggerId = ObservabilityJson.GetStringProperty(trigger, "triggerid");
            var host = GetProperty(trigger, "hosts");
            if (string.IsNullOrEmpty(triggerId) || host.ValueKind != JsonValueKind.Array || host.GetArrayLength() == 0)
            {
                continue;
            }

            var first = host.EnumerateArray().First();
            hostByTrigger[triggerId] = (ObservabilityJson.GetStringProperty(first, "hostid") ?? string.Empty, ObservabilityJson.GetStringProperty(first, "name") ?? string.Empty);
        }

        foreach (var (problem, triggerId) in problems)
        {
            if (hostByTrigger.TryGetValue(triggerId, out var host))
            {
                problem.HostId = host.HostId;
                problem.HostName = host.Name;
            }
        }
    }

    private async Task<ZabbixQueryResponse> HostsAsync(ZabbixQueryRequest request, string credential, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["output"] = "extend",
            ["selectInterfaces"] = new[] { "ip", "dns", "port", "type", "available" },
            ["sortfield"] = "name",
            ["sortorder"] = "ASC",
            ["limit"] = _config.MaxListItems + 1,
        };
        if (!string.IsNullOrWhiteSpace(request.SearchHost))
        {
            parameters["search"] = new Dictionary<string, object?> { ["host"] = request.SearchHost.Trim() };
        }

        var groupIds = ParseGroupIds(request.HostGroupIds);
        if (groupIds.Length > 0)
        {
            parameters["groupids"] = groupIds;
        }

        var hosts = ReadArray(await SendRpcAsync("host.get", parameters, credential, cancellationToken).ConfigureAwait(false), "host.get");
        var truncated = hosts.Count > _config.MaxListItems;
        if (truncated)
        {
            hosts = hosts.GetRange(0, _config.MaxListItems);
        }

        var parsed = hosts.Select(item => new ZabbixHost
        {
            HostId = ObservabilityJson.GetStringProperty(item, "hostid") ?? string.Empty,
            Host = ObservabilityJson.GetStringProperty(item, "host") ?? string.Empty,
            Name = ObservabilityJson.GetStringProperty(item, "name") ?? string.Empty,
            Status = GetIntProperty(item, "status"),
            Interfaces = ReadMaps(GetProperty(item, "interfaces")),
        }).ToList();
        return new ZabbixQueryResponse { ResultType = "hosts", Hosts = parsed, Truncated = truncated };
    }

    private async Task<ZabbixQueryResponse> TriggersAsync(ZabbixQueryRequest request, string credential, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["output"] = "extend",
            ["selectHosts"] = new[] { "hostid", "host", "name" },
            ["only_true"] = true,
            ["monitored"] = true,
            ["active"] = true,
            ["sortfield"] = "lastchange",
            ["sortorder"] = "DESC",
            ["limit"] = _config.MaxListItems + 1,
        };
        if (request.SeverityMin > 0)
        {
            parameters["min_severity"] = Math.Clamp(request.SeverityMin, 1, MaxSeverity);
        }

        var groupIds = ParseGroupIds(request.HostGroupIds);
        if (groupIds.Length > 0)
        {
            parameters["groupids"] = groupIds;
        }

        if (!string.IsNullOrWhiteSpace(request.SearchHost))
        {
            var hostIds = await ResolveHostIdsAsync(request.SearchHost.Trim(), credential, cancellationToken).ConfigureAwait(false);
            if (hostIds.Count == 0)
            {
                return new ZabbixQueryResponse { ResultType = "triggers" };
            }

            parameters["hostids"] = hostIds;
        }

        var triggers = ReadArray(await SendRpcAsync("trigger.get", parameters, credential, cancellationToken).ConfigureAwait(false), "trigger.get");
        var truncated = triggers.Count > _config.MaxListItems;
        if (truncated)
        {
            triggers = triggers.GetRange(0, _config.MaxListItems);
        }

        var parsed = triggers.Select(item =>
        {
            var lastChange = ObservabilityJson.GetStringProperty(item, "lastchange");
            return new ZabbixTrigger
            {
                TriggerId = ObservabilityJson.GetStringProperty(item, "triggerid") ?? string.Empty,
                Description = ObservabilityJson.GetStringProperty(item, "description") ?? string.Empty,
                Priority = GetIntProperty(item, "priority"),
                PriorityName = ZabbixResponseParser.SeverityName(GetIntProperty(item, "priority")),
                Value = GetIntProperty(item, "value"),
                LastChange = ZabbixResponseParser.ClockToIso(lastChange),
                Age = ZabbixResponseParser.AgeFromClock(lastChange),
                Hosts = ReadMaps(GetProperty(item, "hosts")).Select(host => new ZabbixHostRef
                {
                    HostId = host.TryGetValue("hostid", out var hostId) ? hostId : string.Empty,
                    Host = host.TryGetValue("host", out var hostName) ? hostName : string.Empty,
                    Name = host.TryGetValue("name", out var name) ? name : string.Empty,
                }).ToList(),
            };
        }).ToList();
        return new ZabbixQueryResponse { ResultType = "triggers", Triggers = parsed, Truncated = truncated };
    }

    /// <summary>
    /// 主机名模糊搜索换主机 ID 列表（trigger.get 不支持按名称搜索）.
    /// </summary>
    private async Task<List<string>> ResolveHostIdsAsync(string searchHost, string credential, CancellationToken cancellationToken)
    {
        var hosts = ReadArray(await SendRpcAsync(
            "host.get",
            new Dictionary<string, object?>
            {
                ["output"] = new[] { "hostid" },
                ["search"] = new Dictionary<string, object?> { ["host"] = searchHost },
                ["limit"] = MaxMaxListItems,
            },
            credential,
            cancellationToken).ConfigureAwait(false), "host.get");
        return hosts
            .Select(item => ObservabilityJson.GetStringProperty(item, "hostid") ?? string.Empty)
            .Where(id => id.Length > 0)
            .ToList();
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    private static List<JsonElement> ReadArray(JsonElement result, string method)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(502, $"Zabbix {method} 响应形态不是预期结果（应为数组）");
        }

        return result.EnumerateArray().ToList();
    }

    private static List<IReadOnlyDictionary<string, string>> ReadMaps(JsonElement array)
    {
        var result = new List<IReadOnlyDictionary<string, string>>();
        if (array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            var map = ObservabilityJson.ToStringMap(item);
            if (map.Count > 0)
            {
                result.Add(map);
            }
        }

        return result;
    }
}
