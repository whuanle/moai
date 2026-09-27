using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// MySQL 只读查询插件配置（每个实例独立保存）.
/// </summary>
public class MysqlQueryConfig
{
    /// <summary>
    /// MySQL 主机地址.
    /// </summary>
    [Description("MySQL 主机地址，例如 127.0.0.1 或 mysql.example.com")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 端口（1-65535）.
    /// </summary>
    [Description("MySQL 端口，取值 1-65535（默认 3306）")]
    public int Port { get; set; } = 3306;

    /// <summary>
    /// 默认数据库（可选）.
    /// </summary>
    [Description("默认数据库（可选）：未带库名的表按该库解析；建议填写，查询时可省略库名前缀")]
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// 登录用户名.
    /// </summary>
    [Description("登录用户名，建议为插件单独创建只读账号")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码（可选）.
    /// </summary>
    [Description("登录密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 单次最多返回的行数（1-1000）.
    /// </summary>
    [Description("单次最多返回的行数，取值 1-1000（默认 100）；超出部分被丢弃，并把响应中的 Truncated 置为 true")]
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// 语句超时秒数（1-300），作用于客户端 CommandTimeout（超时后驱动会杀掉该查询）.
    /// </summary>
    [Description("语句超时秒数，取值 1-300（默认 30）；超时后由驱动终止查询并返回可读失败")]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
