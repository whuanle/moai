using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 告警条目.
/// </summary>
public class PrometheusAlert
{
    /// <summary>
    /// 告警标签.
    /// </summary>
    [Description("告警标签（含 alertname、instance、severity 等）")]
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 告警注解.
    /// </summary>
    [Description("告警注解（含 summary、description 等）")]
    public IReadOnlyDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 告警状态.
    /// </summary>
    [Description("告警状态：firing 或 pending")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 触发时间.
    /// </summary>
    [Description("处于当前状态的起始时间（RFC3339）")]
    public string ActiveAt { get; set; } = string.Empty;

    /// <summary>
    /// 表达式求值文本.
    /// </summary>
    [Description("表达式求值结果文本")]
    public string Value { get; set; } = string.Empty;
}
