using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
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
/// ClickHouse 查询（动态插件）：对 ClickHouse 执行自由只读 SQL——AI 自行完成「摸库表 → 看结构 → 查数据」的完整链路，不预设查询模板.
/// </summary>
/// <remarks>
/// 只读约束分三层（见 <see cref="ClickHouseReadOnlyGuard"/>）：
/// <list type="number">
/// <item><description>**文本层**：<see cref="ClickHouseReadOnlyGuard.Validate"/> 在通用 SQL 守卫之上拒绝 SYSTEM/ATTACH/EXCHANGE/KILL 与 url(、file(、s3(、remote( 等访问外部数据源的表函数；写操作/DDL/多语句在进守卫时即被拒。</description></item>
/// <item><description>**连接层**：HTTP 请求固定带 <c>readonly=1</c>（服务端拒绝一切数据变更），即使文本校验被绕过也会被 ClickHouse 拒绝。</description></item>
/// <item><description>**资源层**：<c>max_execution_time</c> + 客户端超时 + <c>max_result_rows</c>(+1)/<c>result_overflow_mode=break</c> 与客户端按 <see cref="ClickHouseQueryConfig.MaxRows"/> 截断。</description></item>
/// </list>
/// 表结构与库表发现不设专用模式：AI 用 <c>SHOW DATABASES</c>/<c>SHOW TABLES</c>/<c>DESCRIBE TABLE</c>/<c>SHOW CREATE TABLE</c>/
/// 查 <c>system.tables</c>、<c>system.columns</c> 等只读语句自行完成。出参固定 FORMAT JSON（用户 SQL 末尾自带的 FORMAT 子句被剥离），
/// 列名与列类型来自响应 <c>meta</c>（空结果集也带列信息），服务端 Int64 不加引号、NaN/+Inf 加引号，解析层用 <see cref="ObservabilityJson"/> 归一。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件；客户端为 transient，BaseAddress/超时按实例配置每次重设。
/// </remarks>
[AiPlugin(
    key: "clickhouse_query",
    Name = "ClickHouse 查询",
    Description = "对 ClickHouse 自由执行只读 SQL：先 SHOW DATABASES 摸库、SHOW TABLES FROM 库名 摸表、DESCRIBE TABLE 或 SHOW CREATE TABLE 看结构，再 SELECT ... LIMIT 查询；仅允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN 等读语句，写操作/DDL/会话变更一律拒绝（ClickStack 观测数据请用 clickstack_query 模板）")]
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
              "Sql": "SHOW TABLES FROM otel" // 第一步摸表；随后 DESCRIBE TABLE otel.otel_traces 看列；最后 SELECT Timestamp, ServiceName FROM otel.otel_traces WHERE Timestamp >= now() - INTERVAL 1 HOUR LIMIT 100
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "BaseUrl": "http://192.168.50.199:8123", // ClickHouse HTTP 接口（默认 8123）
              "Username": "default",                   // Basic 认证用户名（建议只读账号）
              "Password": "",                          // Basic 认证密码
              "Database": "",                          // 默认数据库（可选），未带库名的表按该库解析
              "TimeoutSeconds": 30,                    // 单次请求超时秒数，1-300（同时服务端 max_execution_time）
              "MaxRows": 100                           // 最多返回行数，1-1000
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
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<ClickHouseQueryResponse> RunAsync(ClickHouseQueryRequest request, CancellationToken cancellationToken)
    {
        var violation = ClickHouseReadOnlyGuard.Validate(request.Sql);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        var sql = ClickHouseReadOnlyGuard.StripTrailingFormatClause(request.Sql!);
        return await QueryAsync(sql, cancellationToken).ConfigureAwait(false);
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
    /// 执行单条只读查询并解析 FORMAT JSON（列名/列类型来自 meta，行来自 data）.
    /// </summary>
    /// <param name="sql">经守卫与 FORMAT 剥离后的 SQL.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>查询结果.</returns>
    private async Task<ClickHouseQueryResponse> QueryAsync(string sql, CancellationToken cancellationToken)
    {
        PrepareClient();
        var rowLimit = Math.Clamp(_config.MaxRows, MinMaxRows, MaxMaxRows);
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
                "JSON",
                "break",
                null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            throw new BusinessException((int)ex.StatusCode, $"ClickHouse 调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }
        catch (HttpRequestException ex)
        {
            throw new BusinessException(502, $"ClickHouse 连接失败：{ex.Message}");
        }

        using var document = ObservabilityJson.ParseOrThrow(raw, "ClickHouse");
        var root = document.RootElement;

        var columns = new List<string>();
        var columnTypes = new List<string>();
        if (root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in meta.EnumerateArray())
            {
                columns.Add(item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty);
                columnTypes.Add(item.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty);
            }
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var truncated = false;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (rows.Count >= rowLimit)
                {
                    truncated = true;
                    break;
                }

                rows.Add(ToRow(item));
            }
        }

        return new ClickHouseQueryResponse
        {
            Columns = columns,
            ColumnTypes = columnTypes,
            Rows = rows,
            RowCount = rows.Count,
            Truncated = truncated,
        };
    }

    /// <summary>
    /// 把 FORMAT JSON 的一行 data 对象转为「列名 → 值」映射.
    /// </summary>
    /// <param name="element">data 数组元素.</param>
    /// <returns>行映射.</returns>
    private static IReadOnlyDictionary<string, object?> ToRow(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new BusinessException(502, $"ClickHouse 响应的 data 不是对象数组：{ObservabilityJson.Truncate(element.GetRawText(), 200)}");
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            row[property.Name] = ObservabilityJson.ToClr(property.Value);
        }

        return row;
    }
}
