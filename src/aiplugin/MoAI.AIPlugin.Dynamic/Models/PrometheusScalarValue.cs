using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 标量（scalar/string）求值点：值为文本形式以保留 NaN/+Inf/-Inf.
/// </summary>
public class PrometheusScalarValue
{
    /// <summary>
    /// Unix 秒时间戳.
    /// </summary>
    [Description("求值时间（Unix 秒）")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 求值结果（文本）.
    /// </summary>
    [Description("求值结果文本（数值可能带 NaN/+Inf/-Inf）")]
    public string Value { get; set; } = string.Empty;
}
