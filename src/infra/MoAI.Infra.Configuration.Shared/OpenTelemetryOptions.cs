using Microsoft.AspNetCore.Mvc.Formatters;
using MoAI.Infra.Models;

namespace MoAI.Infra;

/// <summary>
/// 可观察性配置.
/// </summary>
public class OpenTelemetryOptions
{
    /// <summary>
    /// 追踪数据的 OTLP 接收器地址.
    /// </summary>
    public string Trace { get; init; } = string.Empty;

    /// <summary>
    /// 指标数据的 OTLP 接收器地址.
    /// </summary>
    public string Metrics { get; init; } = string.Empty;

    /// <summary>
    /// 协议类型，默认使用 HTTP/1.1.
    /// </summary>
    public int Protocol { get; init; }
}