using System;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using Npgsql;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// PostgreSQL 只读查询（动态插件）：用实例配置中的连接串执行单条只读 SQL，返回列名与行数据.
/// </summary>
/// <remarks>
/// 只读约束分三层（见 <see cref="SqlReadOnlyGuard"/>）：
/// <list type="number">
/// <item><description>**文本层**：<see cref="SqlReadOnlyGuard.Validate"/> 只允许单条 READ 语句，拒绝写操作/DDL/会话与事务控制关键字。</description></item>
/// <item><description>**连接层**：连接打开后立即打开会话级只读事务（<c>SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY</c>），即使文本校验被绕过，PostgreSQL 也会以 <c>25006 read_only_sql_transaction</c> 拒绝写操作。</description></item>
/// <item><description>**资源层**：<c>SET statement_timeout</c> 限制服务端执行时长，<c>CommandTimeout</c> 限制客户端等待，行数由 <see cref="PostgresQueryConfig.MaxRows"/> 截断。</description></item>
/// </list>
/// 连接串只来自实例配置（面向用户自有的业务库），不打印、不落日志；每次运行都由 <c>PluginExecutor</c> 创建独立作用域，插件不持有跨请求状态（仅缓存配置）。
/// </remarks>
[AiPlugin(
    key: "postgres_query",
    Name = "PostgreSQL 只读查询",
    Description = "用实例配置中的连接串执行只读 SQL：仅允许 SELECT/WITH/TABLE/VALUES/SHOW/EXPLAIN/DESCRIBE 单条查询，连接以只读事务打开、服务端拒绝写操作，返回列名与行数据")]
public class PostgresQueryPlugin : IDynamicPluginRuntime<PostgresQueryRequest, PostgresQueryResponse, PostgresQueryConfig>
{
    /// <summary>返回行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>返回行数上限的上界.</summary>
    private const int MaxMaxRows = 1000;

    /// <summary>语句超时的下界（秒）.</summary>
    private const int MinCommandTimeoutSeconds = 1;

    /// <summary>语句超时的上界（秒）.</summary>
    private const int MaxCommandTimeoutSeconds = 300;

    /// <summary>
    /// 会话初始化 SQL：开启会话级只读（<c>set_config('default_transaction_read_only', 'on', false)</c> 等价于
    /// <c>SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY</c>）、设置服务端语句超时、标记会话来源.
    /// </summary>
    /// <remarks>语句为固定字面量，超时值以参数传入，不含用户输入（故不触发 CA2100）.</remarks>
    private const string SessionSetupSql = "SELECT set_config('default_transaction_read_only', 'on', false), set_config('statement_timeout', @statementTimeout, false), set_config('application_name', 'moai-aiplugin', false)";

    private PostgresQueryConfig _config = new();

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Sql": "SELECT id, name, created_time FROM public.demo ORDER BY id LIMIT 10" // 只读 SQL：单条查询语句
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ConnectionString": "Host=127.0.0.1;Port=5432;Database=postgres;Username=reader;Password=******", // 建议使用只读账号
              "MaxRows": 100,             // 单次最多返回行数，1-1000
              "CommandTimeoutSeconds": 30 // 语句超时秒数，1-300
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(PostgresQueryConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return Task.FromResult<string?>("数据库连接字符串不能为空");
        }

        _config = new PostgresQueryConfig
        {
            ConnectionString = config.ConnectionString.Trim(),
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
            CommandTimeoutSeconds = Math.Clamp(config.CommandTimeoutSeconds, MinCommandTimeoutSeconds, MaxCommandTimeoutSeconds),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<PostgresQueryResponse> RunAsync(PostgresQueryRequest request, CancellationToken cancellationToken)
    {
        // 1) 文本层只读校验：先于连接，写操作/多条语句在这里就被拒（也不依赖数据库可达）
        var violation = SqlReadOnlyGuard.Validate(request.Sql);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        await using var connection = new NpgsqlConnection(_config.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw new BusinessException(400, $"数据库连接失败：{ex.Message}");
        }

        try
        {
            // 2) 连接层：会话级只读 + 语句超时（语句为固定字面量，超时值以参数传入）
            await using (var session = new NpgsqlCommand(SessionSetupSql, connection))
            {
                session.Parameters.AddWithValue("statementTimeout", (_config.CommandTimeoutSeconds * 1000).ToString(CultureInfo.InvariantCulture));
                await session.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // 3) 执行用户 SQL：文本已由 SqlReadOnlyGuard 校验，服务端会话只读兜底
#pragma warning disable CA2100 // SQL 文本是插件入参：已校验为单条只读语句，且连接处于只读事务
            await using var command = new NpgsqlCommand(request.Sql, connection);
#pragma warning restore CA2100
            command.CommandTimeout = _config.CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var result = await SqlResultReader.ReadAsync(reader, _config.MaxRows, cancellationToken).ConfigureAwait(false);

            return new PostgresQueryResponse
            {
                Columns = result.Columns,
                Rows = result.Rows,
                RowCount = result.Rows.Count,
                Truncated = result.Truncated,
            };
        }
        catch (DbException ex)
        {
            throw new BusinessException(400, $"PostgreSQL 执行失败：{ex.Message}");
        }
    }
}
