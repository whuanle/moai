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
/// MySQL 只读查询（动态插件）：用实例配置中的主机/端口/账号执行单条只读 SQL，返回列名与行数据.
/// </summary>
/// <remarks>
/// 只读约束分三层（见 <see cref="SqlReadOnlyGuard"/>）：
/// <list type="number">
/// <item><description>**文本层**：<see cref="SqlReadOnlyGuard.Validate"/> 只允许单条 READ 语句，拒绝写操作/DDL/会话与事务控制关键字。</description></item>
/// <item><description>**连接层**：连接打开后立即把会话设为只读（<c>SET SESSION TRANSACTION READ ONLY</c>，MySQL 5.6.5+/MariaDB 10.0+），此时对非临时表的写操作会被服务端以 <c>1792</c> 拒绝。</description></item>
/// <item><description>**资源层**：<c>CommandTimeout</c> 由驱动终止超时查询（MySqlConnector 会另开连接 KILL 该查询），行数由 <see cref="MysqlQueryConfig.MaxRows"/> 截断。</description></item>
/// </list>
/// 连接参数只来自实例配置（面向用户自有的业务库），由 <c>InitAsync</c> 经 <see cref="MySqlConnectionStringBuilder"/> 拼成连接串（特殊字符自动转义），不打印、不落日志；每次运行都由 <c>PluginExecutor</c> 创建独立作用域，插件不持有跨请求状态（仅缓存配置与连接串）。
/// </remarks>
[AiPlugin(
    key: "mysql_query",
    Name = "MySQL 只读查询",
    Description = "用实例配置中的主机/端口/账号执行只读 SQL：仅允许 SELECT/WITH/TABLE/VALUES/SHOW/EXPLAIN/DESCRIBE 单条查询，会话设为只读、服务端拒绝写操作，返回列名与行数据")]
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

    /// <summary>端口取值的下界.</summary>
    private const int MinPort = 1;

    /// <summary>端口取值的上界.</summary>
    private const int MaxPort = 65535;

    private MysqlQueryConfig _config = new();

    /// <summary>InitAsync 校验通过后由离散配置字段拼出的连接串（特殊字符由驱动 builder 转义）.</summary>
    private string _connectionString = string.Empty;

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
              "Host": "127.0.0.1",        // MySQL 主机地址
              "Port": 3306,               // 端口，1-65535
              "Database": "demo",         // 默认数据库（可选），查询时可省略库名前缀
              "Username": "reader",       // 登录用户名，建议使用只读账号
              "Password": "******",       // 登录密码（可选）
              "MaxRows": 100,             // 单次最多返回行数，1-1000
              "CommandTimeoutSeconds": 30 // 语句超时秒数，1-300
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(MysqlQueryConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            return Task.FromResult<string?>("数据库主机地址 Host 不能为空");
        }

        if (config.Port < MinPort || config.Port > MaxPort)
        {
            return Task.FromResult<string?>("端口 Port 取值 1-65535");
        }

        if (string.IsNullOrWhiteSpace(config.Username))
        {
            return Task.FromResult<string?>("用户名 Username 不能为空");
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = config.Host.Trim(),
            Port = (uint)config.Port,
            Database = config.Database?.Trim() ?? string.Empty,
            UserID = config.Username.Trim(),
            Password = config.Password ?? string.Empty,
        };
        _connectionString = builder.ConnectionString;

        _config = new MysqlQueryConfig
        {
            Host = config.Host.Trim(),
            Port = config.Port,
            Database = builder.Database,
            Username = config.Username.Trim(),
            Password = config.Password ?? string.Empty,
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

        await using var connection = new MySqlConnection(_connectionString);
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
