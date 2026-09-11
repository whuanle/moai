using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// PostgreSQL 只读查询插件配置（每个实例独立保存）.
/// </summary>
public class PostgresQueryConfig
{
    /// <summary>
    /// PostgreSQL 连接字符串.
    /// </summary>
    [Description("PostgreSQL 连接字符串，例如 Host=127.0.0.1;Port=5432;Database=postgres;Username=reader;Password=***；建议为插件单独创建只读账号")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 单次最多返回的行数（1-1000）.
    /// </summary>
    [Description("单次最多返回的行数，取值 1-1000（默认 100）；超出部分被丢弃，并把响应中的 Truncated 置为 true")]
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// 语句超时秒数（1-300），同时作用于服务端 statement_timeout 与客户端 CommandTimeout.
    /// </summary>
    [Description("语句超时秒数，取值 1-300（默认 30）；超时后查询会被取消并返回可读失败")]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
