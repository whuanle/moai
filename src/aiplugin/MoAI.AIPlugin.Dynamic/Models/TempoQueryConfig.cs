using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Grafana Tempo 链路查询插件配置（每个实例独立保存）.
/// </summary>
public class TempoQueryConfig
{
    /// <summary>
    /// Tempo 服务地址.
    /// </summary>
    [Description("Tempo 服务地址（HTTP 根地址），例如 http://tempo:3200；可含路径前缀")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证用户名（可选）.
    /// </summary>
    [Description("Basic 认证用户名（可选）；与 BearerToken 都未填则匿名访问")]
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
    /// Grafana 多租户租户 id（可空）.
    /// </summary>
    [Description("租户 id（可选）：Grafana 多租户部署时随 x-scope-orgid 头下发；单租户留空")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// TraceQL 检索最多返回链路数（1-100）.
    /// </summary>
    [Description("TraceQL 检索最多返回链路数，取值 1-100（默认 20）")]
    public int MaxTraces { get; set; } = 20;

    /// <summary>
    /// 取单条链路时最多展开的 span 数（1-1000）.
    /// </summary>
    [Description("取单条链路时最多展开的 span 数，取值 1-1000（默认 200）；超出被丢弃并把 Truncated 置为 true")]
    public int MaxSpans { get; set; } = 200;
}
