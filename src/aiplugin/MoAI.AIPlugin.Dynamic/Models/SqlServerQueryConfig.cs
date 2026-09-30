using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SQL Server 只读查询插件配置（每个实例独立保存）.
/// </summary>
public class SqlServerQueryConfig
{
    /// <summary>
    /// SQL Server 主机.
    /// </summary>
    [Description("SQL Server 主机，例如 10.0.0.8 或 sqlserver.internal")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server 端口.
    /// </summary>
    [Description("SQL Server 端口，1-65535（默认 1433）")]
    public int Port { get; set; } = 1433;

    /// <summary>
    /// 默认数据库.
    /// </summary>
    [Description("默认数据库（可选），查询时可省略库名前缀")]
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// 登录用户名.
    /// </summary>
    [Description("登录用户名，建议使用只读账号（db_datareader）")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码.
    /// </summary>
    [Description("登录密码")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 是否信任服务器证书.
    /// </summary>
    [Description("是否信任服务器证书（自签名证书的内网库保持默认 true；false 时要求受信证书链）")]
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>
    /// 单次最多返回行数（1-1000）.
    /// </summary>
    [Description("单次最多返回行数，取值 1-1000（默认 100）；超出部分被丢弃并把 Truncated 置为 true")]
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// 语句超时秒数（1-300）.
    /// </summary>
    [Description("语句超时秒数，取值 1-300（默认 30）")]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
