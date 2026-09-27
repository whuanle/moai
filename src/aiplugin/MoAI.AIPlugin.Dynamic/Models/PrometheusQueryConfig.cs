using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 指标查询插件配置（每个实例独立保存）.
/// </summary>
public class PrometheusQueryConfig
{
    /// <summary>
    /// Prometheus 服务地址（HTTP API 根地址）.
    /// </summary>
    [Description("Prometheus 服务地址（HTTP API 根地址），例如 http://prometheus:9090；可含路径前缀，如 http://proxy:443/prom")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证用户名（可选）.
    /// </summary>
    [Description("Basic 认证用户名（可选）；用户名密码与 BearerToken 都未填则匿名访问")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证密码（可选）.
    /// </summary>
    [Description("Basic 认证密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Bearer Token（可选），已填时优先于用户名密码.
    /// </summary>
    [Description("Bearer Token（可选，如网关 JWT）；已填时优先于用户名密码")]
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 向量/矩阵最多返回的序列条数（1-200）.
    /// </summary>
    [Description("向量/矩阵最多返回的序列条数，取值 1-200（默认 50）；超出部分被丢弃并把 Truncated 置为 true")]
    public int MaxSeries { get; set; } = 50;

    /// <summary>
    /// 区间查询每条序列最多返回的采样点数（1-2000），超出时保留最近 N 点.
    /// </summary>
    [Description("区间查询每条序列最多返回的采样点数，取值 1-2000（默认 500）；超出时丢弃较早点、保留最近 N 点")]
    public int MaxPointsPerSeries { get; set; } = 500;

    /// <summary>
    /// 标签名列表/标签集/告警/规则等列表最多返回条数（1-500）.
    /// </summary>
    [Description("标签名列表/标签集/告警/规则列表等最多返回条数，取值 1-500（默认 200）")]
    public int MaxListItems { get; set; } = 200;
}
