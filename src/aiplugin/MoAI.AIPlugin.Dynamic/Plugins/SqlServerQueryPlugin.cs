using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// SQL Server 只读查询（动态插件）：用实例配置中的主机/端口/账号执行单条只读 SQL，返回列名与行数据.
/// </summary>
/// <remarks>
/// 只读约束（SQL Server 无会话级只读，与 PostgreSQL/MySQL 的三层模型差异见 sdd）：
/// <list type="number">
/// <item><description>**文本层**：<see cref="SqlReadOnlyGuard.Validate"/> 只允许单条 READ 语句，拒绝写操作/DDL/会话与事务控制关键字，校验先于连接。</description></item>
/// <item><description>**账号层**：SQL Server 无 <c>SET SESSION READ ONLY</c> 等价物，真正的保证是登录账号权限——配置示例与 <c>[Description]</c> 均提示「建议使用只读账号（db_datareader）」。</description></item>
/// <item><description>**资源层**：行数由 <see cref="SqlServerQueryConfig.MaxRows"/> 截断，超时由驱动 <c>CommandTimeout</c> 终止。</description></item>
/// </list>
/// 连接参数只来自实例配置（面向用户自有的业务库），由 <c>InitAsync</c> 经 <see cref="SqlConnectionStringBuilder"/> 拼成连接串（特殊字符自动转义），不打印、不落日志；内网自签名证书默认 <c>TrustServerCertificate=true</c> 可关。每次运行都由 <c>PluginExecutor</c> 创建独立作用域，插件不持有跨请求状态.
/// </remarks>
[AiPlugin(
    key: "sqlserver_query",
    Name = "SQL Server 只读查询",
    Description = "用实例配置中的主机/端口/账号执行只读 SQL：仅允许 SELECT/WITH/TABLE/VALUES/SHOW/EXPLAIN/DESCRIBE 单条查询，建议使用只读账号（db_datareader），返回列名与行数据")]
public class SqlServerQueryPlugin : IDynamicPluginRuntime<SqlServerQueryRequest, SqlServerQueryResponse, SqlServerQueryConfig>
{
    /// <summary>返回行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>返回行数上限的上界.</summary>
    private const int MaxMaxRows = 1000;

    /// <summary>语句超时的下界（秒）.</summary>
    private const int MinCommandTimeoutSeconds = 1;

    /// <summary>语句超时的上界（秒）.</summary>
    private const int MaxCommandTimeoutSeconds = 300;

    /// <summary>端口取值的下界.</summary>
    private const int MinPort = 1;

    /// <summary>端口取值的上界.</summary>
    private const int MaxPort = 65535;

    private SqlServerQueryConfig _config = new();

    /// <summary>InitAsync 校验通过后由离散配置字段拼出的连接串（特殊字符由驱动 builder 转义）.</summary>
    private string _connectionString = string.Empty;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Sql": "SELECT TOP 10 id, name, created_time FROM demo ORDER BY id" // 只读 SQL：单条查询语句
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "Host": "10.0.0.8",               // SQL Server 主机
              "Port": 1433,                     // 端口，1-65535
              "Database": "demo",               // 默认数据库（可选）
              "Username": "reader",             // 登录用户名，建议只读账号（db_datareader）
              "Password": "******",             // 登录密码
              "TrustServerCertificate": true,   // 信任服务器证书（自签名内网库保持 true）
              "MaxRows": 100,                   // 单次最多返回行数，1-1000
              "CommandTimeoutSeconds": 30       // 语句超时秒数，1-300
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(SqlServerQueryConfig config)
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

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{config.Host.Trim()},{config.Port}",
            InitialCatalog = config.Database?.Trim() ?? string.Empty,
            UserID = config.Username.Trim(),
            Password = config.Password ?? string.Empty,
            TrustServerCertificate = config.TrustServerCertificate,
        };
        _connectionString = builder.ConnectionString;

        _config = new SqlServerQueryConfig
        {
            Host = config.Host.Trim(),
            Port = config.Port,
            Database = builder.InitialCatalog,
            Username = config.Username.Trim(),
            Password = config.Password ?? string.Empty,
            TrustServerCertificate = config.TrustServerCertificate,
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
            CommandTimeoutSeconds = Math.Clamp(config.CommandTimeoutSeconds, MinCommandTimeoutSeconds, MaxCommandTimeoutSeconds),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<SqlServerQueryResponse> RunAsync(SqlServerQueryRequest request, CancellationToken cancellationToken)
    {
        // 文本层只读校验：先于连接，写操作/多条语句在这里就被拒（也不依赖数据库可达）
        var violation = SqlReadOnlyGuard.Validate(request.Sql);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        await using var connection = new SqlConnection(_connectionString);
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
            // SQL 文本已由 SqlReadOnlyGuard 校验；SQL Server 无会话级只读，账号权限是最终保证（见 remarks）
#pragma warning disable CA2100 // SQL 文本是插件入参：已校验为单条只读语句
            await using var command = new SqlCommand(request.Sql, connection);
#pragma warning restore CA2100
            command.CommandTimeout = _config.CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var result = await SqlResultReader.ReadAsync(reader, _config.MaxRows, cancellationToken).ConfigureAwait(false);

            return new SqlServerQueryResponse
            {
                Columns = result.Columns,
                Rows = result.Rows,
                RowCount = result.Rows.Count,
                Truncated = result.Truncated,
            };
        }
        catch (DbException ex)
        {
            throw new BusinessException(400, $"SQL Server 执行失败：{ex.Message}");
        }
    }
}
