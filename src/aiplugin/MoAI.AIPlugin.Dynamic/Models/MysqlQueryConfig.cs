using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// MySQL 只读查询插件配置（每个实例独立保存）.
/// </summary>
public class MysqlQueryConfig
{
    /// <summary>
    /// MySQL 连接字符串.
    /// </summary>
    [Description("MySQL 连接字符串，例如 Server=127.0.0.1;Port=3306;Database=demo;Uid=reader;Pwd=***;Connection Timeout=10；建议为插件单独创建只读账号")]
    public string ConnectionString { get; set; } = string.Empty;

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
