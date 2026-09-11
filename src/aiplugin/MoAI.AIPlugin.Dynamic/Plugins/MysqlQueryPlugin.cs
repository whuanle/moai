using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MySqlConnector;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// MySQL 只读查询（动态插件）：用实例配置中的连接串执行单条只读 SQL，返回列名与行数据.
/// </summary>
/// <remarks>
/// 只读约束分三层（见 <see cref="SqlReadOnlyGuard"/>）：
/// <list type="number">
/// <item><description>**文本层**：<see cref="SqlReadOnlyGuard.Validate"/> 只允许单条 READ 语句，拒绝写操作/DDL/会话与事务控制关键字。</description></item>
/// <item><description>**连接层**：连接打开后立即把会话设为只读（<c>SET SESSION TRANSACTION READ ONLY</c>，MySQL 5.6.5+/MariaDB 10.0+），此时对非临时表的写操作会被服务端以 <c>1792</c> 拒绝。</description></item>
/// <item><description>**资源层**：<c>CommandTimeout</c> 由驱动终止超时查询（MySqlConnector 会另开连接 KILL 该查询），行数由 <see cref="MysqlQueryConfig.MaxRows"/> 截断。</description></item>
/// </list>
/// 连接串只来自实例配置（面向用户自有的业务库），不打印、不落日志；每次运行都由 <c>PluginExecutor</c> 创建独立作用域，插件不持有跨请求状态（仅缓存配置）。
/// </remarks>
[AiPlugin(
    key: "mysql_query",
    Name = "MySQL 只读查询",
    Description = "用实例配置中的连接串执行只读 SQL：仅允许 SELECT/WITH/TABLE/VALUES/SHOW/EXPLAIN/DESCRIBE 单条查询，会话设为只读、服务端拒绝写操作，返回列名与行数据")]
public class MysqlQueryPlugin : IDynamicPluginRuntime<MysqlQueryRequest, MysqlQueryResponse, MysqlQueryConfig>
{
    /// <summary>返回行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>返回行数上限的上界.</summary>
    private const int MaxMaxRows = 1000;

    /// <summary>语句超时的下界（秒）.</summary>
    private const int MinCommandTimeoutSeconds = 1;

    /// <summary>语句超时的上界（秒）.</summary>
    private const int MaxCommandTimeoutSeconds = 300;

    /// <summary>会话级只读：后续事务一律只读（对非临时表的写操作被服务端拒绝）.</summary>
    private const string ReadOnlySessionSql = "SET SESSION TRANSACTION READ ONLY";

    private MysqlQueryConfig _config = new();

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Sql": "SELECT id, name, created_time FROM demo ORDER BY id LIMIT 10" // 只读 SQL：单条查询语句
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "ConnectionString": "Server=127.0.0.1;Port=3306;Database=demo;Uid=reader;Pwd=******;Connection Timeout=10", // 建议使用只读账号
              "MaxRows": 100,             // 单次最多返回行数，1-1000
              "CommandTimeoutSeconds": 30 // 语句超时秒数，1-300
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(MysqlQueryConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return Task.FromResult<string?>("数据库连接字符串不能为空");
        }

        _config = new MysqlQueryConfig
        {
            ConnectionString = config.ConnectionString.Trim(),
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
            CommandTimeoutSeconds = Math.Clamp(config.CommandTimeoutSeconds, MinCommandTimeoutSeconds, MaxCommandTimeoutSeconds),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<MysqlQueryResponse> RunAsync(MysqlQueryRequest request, CancellationToken cancellationToken)
    {
        // 1) 文本层只读校验：先于连接，写操作/多条语句在这里就被拒（也不依赖数据库可达）
        var violation = SqlReadOnlyGuard.Validate(request.Sql);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        await using var connection = new MySqlConnection(_config.ConnectionString);
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
            // 2) 连接层：会话级只读（MySqlConnector 不支持一次下发多条语句，故单条执行）
            await using (var session = new MySqlCommand(ReadOnlySessionSql, connection))
            {
                await session.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // 3) 执行用户 SQL：文本已由 SqlReadOnlyGuard 校验，服务端会话只读兜底
#pragma warning disable CA2100 // SQL 文本是插件入参：已校验为单条只读语句，且会话处于只读事务
            await using var command = new MySqlCommand(request.Sql, connection);
#pragma warning restore CA2100
            command.CommandTimeout = _config.CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var result = await SqlResultReader.ReadAsync(reader, _config.MaxRows, cancellationToken).ConfigureAwait(false);

            return new MysqlQueryResponse
            {
                Columns = result.Columns,
                Rows = result.Rows,
                RowCount = result.Rows.Count,
                Truncated = result.Truncated,
            };
        }
        catch (DbException ex)
        {
            throw new BusinessException(400, $"MySQL 执行失败：{ex.Message}");
        }
    }
}
