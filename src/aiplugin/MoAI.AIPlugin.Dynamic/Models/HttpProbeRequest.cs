using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 可用性拨测插件请求参数.
/// </summary>
public class HttpProbeRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：http=URL 可用性（默认）/ tcp=端口连通 / dns=域名解析；未知值会被拒绝")]
    public string Mode { get; set; } = "http";

    /// <summary>
    /// http 模式：目标 URL.
    /// </summary>
    [Description("http 模式：目标 URL（http:// 或 https://），例如 https://example.com/healthz")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// http 模式：请求方法.
    /// </summary>
    [Description("http 模式：请求方法，仅允许 GET/HEAD（默认 GET）")]
    public string Method { get; set; } = "GET";

    /// <summary>
    /// tcp/dns 模式：目标主机.
    /// </summary>
    [Description("tcp/dns 模式：目标主机（域名或 IP），例如 example.com 或 10.0.0.5")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// tcp 模式：目标端口.
    /// </summary>
    [Description("tcp 模式：目标端口，1-65535")]
    public int Port { get; set; }
}
