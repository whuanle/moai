using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Redis 只读诊断插件配置（每个实例独立保存）.
/// </summary>
public class RedisQueryConfig
{
    /// <summary>
    /// Redis 主机.
    /// </summary>
    [Description("Redis 主机，例如 127.0.0.1 或 redis.internal")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Redis 端口.
    /// </summary>
    [Description("Redis 端口，1-65535（默认 6379）")]
    public int Port { get; set; } = 6379;

    /// <summary>
    /// 用户名（可选）.
    /// </summary>
    [Description("用户名（可选，Redis 6+ ACL）")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（可选）.
    /// </summary>
    [Description("密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用 TLS.
    /// </summary>
    [Description("是否使用 TLS（默认 false）")]
    public bool Ssl { get; set; }

    /// <summary>
    /// 逻辑库编号（0-15）.
    /// </summary>
    [Description("逻辑库编号，0-15（默认 0）")]
    public int Database { get; set; }

    /// <summary>
    /// 连接与命令超时秒数（1-120）.
    /// </summary>
    [Description("连接与命令超时秒数，取值 1-120（默认 10）")]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 列表类结果最多返回条数（1-500）.
    /// </summary>
    [Description("slowlog/client_list 等列表最多返回条数，取值 1-500（默认 100）")]
    public int MaxListItems { get; set; } = 100;
}
