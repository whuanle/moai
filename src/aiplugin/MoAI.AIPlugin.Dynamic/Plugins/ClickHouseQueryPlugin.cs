using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.ClickHouse;
using MoAI.Infra.Exceptions;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// ClickHouse 查询（动态插件）：用实例配置中的 HTTP 地址执行只读查询表达式，或按筛选参数便捷查询 OpenTelemetry 链路/日志/指标数据.
/// </summary>
/// <remarks>
/// 只读约束分三层（见 <see cref="ClickHouseReadOnlyGuard"/>）：
/// <list type="number">
/// <item><description>**文本层**：<see cref="ClickHouseReadOnlyGuard.Validate"/> 在通用 SQL 守卫之上拒绝 SYSTEM/ATTACH/EXCHANGE/KILL 与 url(、file(、s3(、remote( 等访问外部数据源的表函数。</description></item>
/// <item><description>**连接层**：HTTP 请求固定带 <c>readonly=1</c>（服务端拒绝一切数据变更），即使文本校验被绕过也会被 ClickHouse 拒绝。</description></item>
/// <item><description>**资源层**：<c>max_execution_time</c> + 客户端超时 + <c>max_result_rows</c>(+1)/<c>result_overflow_mode=break</c> 与客户端按 <see cref="ClickHouseQueryConfig.MaxRows"/> 截断。</description></item>
/// </list>
/// 出参固定 JSONEachRow（用户 SQL 末尾自带的 FORMAT 子句被剥离）；服务端 Int64 不加引号、NaN/+Inf/-Inf 加引号，
/// 解析层用 <see cref="ObservabilityJson"/> 归一。OTel 便捷模式依赖 <c>system.columns</c> 自适配 schema（老版本
/// 缺 StatusCode/SpanAttributes 等列时自动忽略并在 Notice 说明）。每次运行由 <c>PluginExecutor</c> 创建独立
/// 作用域实例化插件；客户端为 transient，BaseAddress/超时按实例配置每次重设。
/// </remarks>
[AiPlugin(
    key: "clickhouse_query",
    Name = "ClickHouse 查询",
    Description = "对 ClickHouse 执行只读查询（Mode=sql，如 SELECT …）或便捷查询 OpenTelemetry 数据（Mode=traces/logs/metrics，从 otel 库按 TraceId/服务名/时间过滤）；先用 {\"Mode\":\"sql\",\"Sql\":\"SHOW TABLES FROM otel\"} 摸底")]
public class ClickHouseQueryPlugin : IDynamicPluginRuntime<ClickHouseQueryRequest, ClickHouseQueryResponse, ClickHouseQueryConfig>
{
    /// <summary>请求超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>请求超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 300;

    /// <summary>返回行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>返回行数上限的上界.</summary>
    private const int MaxMaxRows = 1000;

    /// <summary>schema 自描述查询（system.columns/system.tables）的行数上限.</summary>
    private const int MaxDiscoveryRows = 200;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "sql", "traces", "logs", "metrics",
    };

    private static readonly string[] TracePreferredColumns =
    [
        "Timestamp", "TraceId", "SpanId", "ParentSpanId", "ServiceName", "Name", "Kind", "StatusCode", "Duration", "ResourceAttributes",
    ];

    private static readonly string[] LogPreferredColumns =
    [
        "Timestamp", "TraceId", "SpanId", "SeverityText", "SeverityNumber", "ServiceName", "Body", "ResourceAttributes",
    ];

    private static readonly string[] MetricPreferredColumns =
    [
        "MetricName", "ServiceName", "Timestamp", "Value", "Count", "Min", "Max", "Sum", "Last", "ResourceAttributes",
    ];

    private readonly IClickHouseClient _client;
    private ClickHouseQueryConfig _config = new();
    private bool _prepared;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickHouseQueryPlugin"/> class.
    /// </summary>
    /// <param name="client">ClickHouse API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public ClickHouseQueryPlugin(IClickHouseClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "sql",              // sql | traces | logs | metrics
              "Sql": "SELECT Timestamp, TraceId, ServiceName, Name, Duration FROM otel.otel_traces ORDER BY Timestamp DESC LIMIT 20", // sql 模式：只读表达式
              "TraceId": "1234aaaa5678bbb",        // traces 模式：按 TraceID 过滤
              "ServiceName": "api",                // traces/logs/metrics：按服务名过滤
              "SpanName": "GET /api/orders",       // traces 模式：按 span 名称过滤
              "MinStatusCode": 2,                  // traces 模式：StatusCode >= 该值（异常 span）
              "SeverityText": "ERROR",             // logs 模式：按严重级过滤
              "SearchText": "timeout",             // logs 模式：Body 子串搜索（Body 为 String 列时生效）
              "MetricName": "http_requests_total", // metrics 模式：按指标名过滤
              "Table": "",                         // metrics 模式：otel_metrics_* 表名（缺省自动选择）
              "TimeFrom": "2026-09-26T08:00:00+08:00", // 开始时间（RFC3339）
              "TimeTo": "2026-09-26T10:00:00+08:00",   // 结束时间（RFC3339）
              "Limit": 50                          // 最多返回行数，1-1000
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://clickhouse:8123", // ClickHouse HTTP 接口地址（默认 8123）
              "Username": "",                      // Basic 认证用户名（可选），建议只读账号
              "Password": "",                      // Basic 认证密码（可选）
              "Database": "",                      // 默认数据库（可选），留空用服务端默认
              "OtelDatabase": "otel",              // OpenTelemetry 数据所在库（otel_collector 导出默认 otel）
              "TimeoutSeconds": 30,                // 单次请求超时秒数，1-300（同时服务端 max_execution_time）
              "MaxRows": 100                       // 最多返回行数，1-1000
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(ClickHouseQueryConfig config)
    {
        var baseUrlValidation = ValidateBaseUrl(config.BaseUrl);
        if (baseUrlValidation != null)
        {
            return Task.FromResult<string?>(baseUrlValidation);
        }

        _config = new ClickHouseQueryConfig
        {
            BaseUrl = EnsureTrailingSlash(config.BaseUrl.Trim()),
            Username = config.Username.Trim(),
            Password = config.Password,
            Database = config.Database.Trim(),
            OtelDatabase = string.IsNullOrWhiteSpace(config.OtelDatabase) ? "otel" : config.OtelDatabase.Trim(),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<ClickHouseQueryResponse> RunAsync(ClickHouseQueryRequest request, CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode.Length == 0)
        {
            mode = "sql";
        }

        if (!Modes.Contains(mode))
        {
            throw new BusinessException(400, $"不支持的 Mode={mode}：仅允许 sql/traces/logs/metrics");
        }

        return mode switch
        {
            "sql" => await RunSqlAsync(request.Sql, cancellationToken).ConfigureAwait(false),
            "traces" => await RunOtelModeAsync("traces", "otel_traces", request, cancellationToken).ConfigureAwait(false),
            "logs" => await RunOtelModeAsync("logs", "otel_logs", request, cancellationToken).ConfigureAwait(false),
            _ => await RunMetricsModeAsync(request, cancellationToken).ConfigureAwait(false),
        };
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
    /// 每次执行前按实例配置设置 BaseAddress 与超时（客户端为 transient，运行之间互不干扰）；
    /// 实例生命周期内只设一次（HttpClient 首请求后属性不可再改）.
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
    /// 引用 ClickHouse 标识符（库名/表名来自实例配置，双写反引号防注入）.
    /// </summary>
    /// <param name="identifier">标识符.</param>
    /// <returns>反引号包裹的标识符.</returns>
    private static string QuoteIdentifier(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private async Task<ClickHouseQueryResponse> RunSqlAsync(string? sql, CancellationToken cancellationToken)
    {
        var violation = ClickHouseReadOnlyGuard.Validate(sql);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        return await QueryCoreAsync(ClickHouseReadOnlyGuard.StripTrailingFormatClause(sql!), null, null, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// OpenTelemetry 便捷查询：自适配 schema 后按过滤参数构造 SQL（值经 $name 参数绑定）.
    /// </summary>
    /// <param name="mode">traces/logs/metrics.</param>
    /// <param name="table">目标表名.</param>
    /// <param name="request">请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>查询结果.</returns>
    private async Task<ClickHouseQueryResponse> RunOtelModeAsync(string mode, string table, ClickHouseQueryRequest request, CancellationToken cancellationToken)
    {
        var preferred = mode switch
        {
            "traces" => TracePreferredColumns,
            "logs" => LogPreferredColumns,
            _ => MetricPreferredColumns,
        };

        var discovered = await DiscoverColumnsAsync(table, cancellationToken).ConfigureAwait(false);
        var selected = BuildSelectedColumns(preferred, discovered);
        if (selected.Count == 0)
        {
            throw new BusinessException(502, $"表 {QuoteIdentifier(_config.OtelDatabase)}.{QuoteIdentifier(table)} 的列无法识别，可能该库不是 OpenTelemetry 导出 schema");
        }

        var missing = CollectMissingColumns(preferred, discovered);
        var (wheres, parameters, notices) = BuildWhere(mode, request, discovered);

        var builder = new StringBuilder();
        builder.Append("SELECT ").Append(string.Join(", ", selected)).Append(" FROM ")
            .Append(QuoteIdentifier(_config.OtelDatabase)).Append('.').Append(QuoteIdentifier(table));
        if (wheres.Count > 0)
        {
            builder.Append(" WHERE ").Append(string.Join(" AND ", wheres));
        }

        if (discovered.ContainsKey("Timestamp"))
        {
            builder.Append(" ORDER BY Timestamp DESC");
        }

        builder.Append(" LIMIT ").Append(ResolveLimit(request.Limit).ToString(CultureInfo.InvariantCulture));

        var response = await QueryCoreAsync(builder.ToString(), parameters, selected, discovered, cancellationToken).ConfigureAwait(false);
        var noticeParts = new List<string>();
        if (missing.Count > 0)
        {
            noticeParts.Add($"schema 缺列（已忽略）：{string.Join(", ", missing)}");
        }

        noticeParts.AddRange(notices);
        noticeParts.Insert(0, $"从 system.columns 自适配选中列：{string.Join(", ", selected)}");
        if (noticeParts.Count > 0)
        {
            response.Notice = string.Join("；", noticeParts);
        }

        return response;
    }

    /// <summary>
    /// otel 便捷模式的过滤条件（全部经 $name 参数绑定，值不进 SQL 文本）.
    /// </summary>
    /// <returns>where 表达式、参数绑定与防御性说明.</returns>
    private (List<string> Wheres, Dictionary<string, string?> Parameters, List<string> Notices) BuildWhere(
        string mode,
        ClickHouseQueryRequest request,
        Dictionary<string, string> discovered)
    {
        var wheres = new List<string>();
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
        var notices = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.TraceId) && discovered.ContainsKey("TraceId"))
        {
            wheres.Add("TraceId = $pTraceId");
            parameters["param_pTraceId"] = request.TraceId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceName) && discovered.ContainsKey("ServiceName"))
        {
            wheres.Add("ServiceName = $pServiceName");
            parameters["param_pServiceName"] = request.ServiceName.Trim();
        }

        if (mode == "traces")
        {
            if (!string.IsNullOrWhiteSpace(request.SpanName) && discovered.ContainsKey("Name"))
            {
                wheres.Add("Name = $pSpanName");
                parameters["param_pSpanName"] = request.SpanName.Trim();
            }

            if (request.MinStatusCode > 0 && discovered.ContainsKey("StatusCode"))
            {
                wheres.Add("StatusCode >= $pStatusCode");
                parameters["param_pStatusCode"] = request.MinStatusCode.ToString(CultureInfo.InvariantCulture);
            }
        }
        else if (mode == "logs")
        {
            if (!string.IsNullOrWhiteSpace(request.SeverityText) && discovered.ContainsKey("SeverityText"))
            {
                wheres.Add("SeverityText = $pSeverityText");
                parameters["param_pSeverityText"] = request.SeverityText.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.SearchText) && discovered.ContainsKey("Body"))
            {
                var bodyType = discovered["Body"];
                if (!bodyType.Contains("Map", StringComparison.OrdinalIgnoreCase))
                {
                    wheres.Add("positionCaseInsensitive(Body, $pSearchText) > 0");
                    parameters["param_pSearchText"] = request.SearchText.Trim();
                }
                else
                {
                    notices.Add("Body 列为 Map 形态，SearchText 已忽略");
                }
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.MetricName) && discovered.ContainsKey("MetricName"))
            {
                wheres.Add("MetricName = $pMetricName");
                parameters["param_pMetricName"] = request.MetricName.Trim();
            }
        }

        AppendTimeRange(discovered, request, wheres, parameters);
        return (wheres, parameters, notices);
    }

    private void AppendTimeRange(Dictionary<string, string> discovered, ClickHouseQueryRequest request, List<string> wheres, Dictionary<string, string?> parameters)
    {
        if (discovered.ContainsKey("Timestamp") && !string.IsNullOrWhiteSpace(request.TimeFrom))
        {
            wheres.Add("Timestamp >= parseDateTime64BestEffort($pTimeFrom)");
            parameters["param_pTimeFrom"] = request.TimeFrom.Trim();
        }

        if (discovered.ContainsKey("Timestamp") && !string.IsNullOrWhiteSpace(request.TimeTo))
        {
            wheres.Add("Timestamp <= parseDateTime64BestEffort($pTimeTo)");
            parameters["param_pTimeTo"] = request.TimeTo.Trim();
        }
    }

    /// <summary>
    /// metrics 模式：先发现 otel_metrics_* 表，再按过滤参数便捷查询.
    /// </summary>
    private async Task<ClickHouseQueryResponse> RunMetricsModeAsync(ClickHouseQueryRequest request, CancellationToken cancellationToken)
    {
        var tables = await DiscoverMetricTablesAsync(cancellationToken).ConfigureAwait(false);
        if (tables.Count == 0)
        {
            throw new BusinessException(400, $"库 {QuoteIdentifier(_config.OtelDatabase)} 中没有 otel_metrics_* 表：可能该 ClickHouse 未接入 OpenTelemetry 指标（检查 otel collector 与 OtelDatabase 配置）");
        }

        var requestTable = request.Table?.Trim();
        string table;
        if (string.IsNullOrWhiteSpace(requestTable))
        {
            if (tables.Count > 1)
            {
                throw new BusinessException(400, $"库 {QuoteIdentifier(_config.OtelDatabase)} 有多张指标表，请指定 Table 参数（候选：{string.Join(", ", tables)}）");
            }

            table = tables[0];
        }
        else if (tables.Contains(requestTable))
        {
            table = requestTable;
        }
        else
        {
            throw new BusinessException(400, $"指标表 {QuoteIdentifier(requestTable)} 不存在（候选：{string.Join(", ", tables)}）");
        }

        return await RunOtelModeAsync("metrics", table, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发现 otel 库里的指标表（otel_metrics_*）.
    /// </summary>
    private async Task<List<string>> DiscoverMetricTablesAsync(CancellationToken cancellationToken)
    {
        var sql = "SELECT name FROM system.tables WHERE database = $pDb AND name LIKE 'otel_metrics%' ORDER BY name";
        var parameters = new Dictionary<string, string?> { ["param_pDb"] = _config.OtelDatabase };
        var result = await ExecuteReadAsync(sql, parameters, MaxDiscoveryRows, cancellationToken).ConfigureAwait(false);
        var tables = new List<string>();
        foreach (var row in result.Rows)
        {
            if (row.TryGetValue("name", out var nameValue) && nameValue is string name && name.Length > 0)
            {
                tables.Add(name);
            }
        }

        return tables;
    }

    /// <summary>
    /// 从 system.columns 发现表的列与类型（表不存在时抛可读错误）.
    /// </summary>
    private async Task<Dictionary<string, string>> DiscoverColumnsAsync(string table, CancellationToken cancellationToken)
    {
        var sql = "SELECT name, type FROM system.columns WHERE database = $pDb AND table = $pTable ORDER BY position";
        var parameters = new Dictionary<string, string?>
        {
            ["param_pDb"] = _config.OtelDatabase,
            ["param_pTable"] = table,
        };
        var result = await ExecuteReadAsync(sql, parameters, MaxDiscoveryRows, cancellationToken).ConfigureAwait(false);
        if (result.Rows.Count == 0)
        {
            throw new BusinessException(400, $"表 {QuoteIdentifier(_config.OtelDatabase)}.{QuoteIdentifier(table)} 不存在：可能该库未接入 OpenTelemetry 数据（检查 OtelDatabase 配置）");
        }

        var discovered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in result.Rows)
        {
            if (row.TryGetValue("name", out var nameValue) && nameValue is string name
                && row.TryGetValue("type", out var typeValue) && typeValue is string type)
            {
                discovered[name] = type;
            }
        }

        return discovered;
    }

    private static List<string> CollectMissingColumns(string[] preferred, Dictionary<string, string> discovered)
    {
        var missing = new List<string>();
        foreach (var column in preferred)
        {
            if (!discovered.ContainsKey(column))
            {
                missing.Add(column);
            }
        }

        return missing;
    }

    /// <summary>
    /// 按偏好序挑选存在的列（属性列只取其 —— SpanAttributes/Attributes/LogAttributes/Labels 中第一个存在的，避免重复噪音）.
    /// </summary>
    private static List<string> BuildSelectedColumns(string[] preferred, Dictionary<string, string> discovered)
    {
        var selected = new List<string>();
        foreach (var column in preferred)
        {
            if (!discovered.ContainsKey(column))
            {
                continue;
            }

            if (column is "SpanAttributes" or "Attributes" or "LogAttributes" or "Labels")
            {
                if (selected.Exists(x => x is "SpanAttributes" or "Attributes" or "LogAttributes" or "Labels"))
                {
                    continue;
                }
            }

            if (!selected.Contains(column))
            {
                selected.Add(column);
            }
        }

        return selected;
    }

    private int ResolveLimit(int? limit)
    {
        return Math.Clamp(limit ?? _config.MaxRows, MinMaxRows, _config.MaxRows);
    }

    /// <summary>
    /// 执行查询并组装响应（列名/类型对齐）.
    /// </summary>
    private async Task<ClickHouseQueryResponse> QueryCoreAsync(
        string sql,
        Dictionary<string, string?>? parameters,
        IReadOnlyList<string>? preferredColumns,
        Dictionary<string, string>? columnTypes,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteReadAsync(sql, parameters, null, cancellationToken).ConfigureAwait(false);
        var columns = new List<string>(preferredColumns ?? []);
        foreach (var key in result.Rows.Count > 0 ? result.Rows[0].Keys : [])
        {
            if (!columns.Contains(key))
            {
                columns.Add(key);
            }
        }

        var types = new List<string>();
        if (columnTypes != null)
        {
            foreach (var column in columns)
            {
                types.Add(columnTypes.TryGetValue(column, out var type) ? type : string.Empty);
            }
        }

        return new ClickHouseQueryResponse
        {
            Columns = columns,
            ColumnTypes = types,
            Rows = result.Rows,
            RowCount = result.Rows.Count,
            Truncated = result.Truncated,
        };
    }

    /// <summary>
    /// 执行一条只读查询并解析逐行 JSONEachRow.
    /// </summary>
    /// <param name="sql">查询表达式.</param>
    /// <param name="parameters">$name 参数绑定.</param>
    /// <param name="maxRows">返回行数上限（null 用配置 MaxRows）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>查询结果的中立表示.</returns>
    private async Task<(IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, bool Truncated)> ExecuteReadAsync(
        string sql,
        Dictionary<string, string?>? parameters,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        PrepareClient();
        var rowLimit = Math.Clamp(maxRows ?? _config.MaxRows, MinMaxRows, MaxMaxRows);
        var authorization = OpsAuthorization.Build(_config.Username, _config.Password, null);

        string raw;
        try
        {
            raw = await _client.QueryAsync(
                authorization,
                sql,
                _config.Database.Length > 0 ? _config.Database : null,
                _config.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
                rowLimit + 1,
                1,
                0,
                1,
                "JSONEachRow",
                "break",
                parameters,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            throw new BusinessException((int)ex.StatusCode, $"ClickHouse 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var truncated = false;
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rows.Count >= rowLimit)
            {
                truncated = true;
                break;
            }

            rows.Add(ParseRow(line));
        }

        return (rows, truncated);
    }

    private static IReadOnlyDictionary<string, object?> ParseRow(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                row[property.Name] = ObservabilityJson.ToClr(property.Value);
            }

            return row;
        }
        catch (JsonException)
        {
            throw new BusinessException(502, $"ClickHouse 响应不是合法 JSONEachRow：{ObservabilityJson.Truncate(line, 200)}");
        }
    }
}


