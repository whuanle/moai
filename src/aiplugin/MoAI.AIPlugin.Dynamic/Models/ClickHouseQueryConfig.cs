using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// ClickHouse 查询插件配置（每个实例独立保存）.
/// </summary>
public class ClickHouseQueryConfig
{
    /// <summary>
    /// ClickHouse HTTP 接口地址.
    /// </summary>
    [Description("ClickHouse HTTP 接口地址（默认 8123 端口），例如 http://192.168.50.199:8123；可含路径前缀")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证用户名（可选）.
    /// </summary>
    [Description("Basic 认证用户名（可选），建议使用只读账号；未填则匿名访问")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证密码（可选）.
    /// </summary>
    [Description("Basic 认证密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 默认数据库（可选）.
    /// </summary>
    [Description("默认数据库（可选）：未指定库名的表按该库解析；留空用服务端默认库")]
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）；同时下发给服务端 max_execution_time")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 最多返回的行数（1-1000）.
    /// </summary>
    [Description("最多返回的行数，取值 1-1000（默认 100）；超出部分被丢弃并把 Truncated 置为 true")]
    public int MaxRows { get; set; } = 100;
}
