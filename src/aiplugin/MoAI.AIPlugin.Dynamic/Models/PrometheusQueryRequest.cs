using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 指标查询插件请求参数.
/// </summary>
public class PrometheusQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：instant=单点 PromQL 求值（默认）/ range=区间求值 / labels=标签名列表 / label_values=某标签的取值 / series=按选择器列标签集 / alerts=当前告警 / rules=规则组；未知值会被拒绝")]
    public string Mode { get; set; } = "instant";

    /// <summary>
    /// PromQL 表达式（instant/range 模式必填）.
    /// </summary>
    [Description("PromQL 表达式（instant/range 模式必填），例如 up、rate(http_requests_total[5m])、sum by (job) (node_cpu_seconds_total)")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// instant 模式的求值时间（可空，缺省当前时间）.
    /// </summary>
    [Description("instant 模式的求值时间（RFC3339 或 Unix 秒），缺省为当前时间")]
    public string? Time { get; set; }

    /// <summary>
    /// range 模式的开始时间（可空，缺省向历史回看 1 小时）.
    /// </summary>
    [Description("range 模式的开始时间（RFC3339 或 Unix 秒）；Start/End/Step 全缺省时不发该字段")]
    public string? Start { get; set; }

    /// <summary>
    /// range 模式的结束时间（可空，缺省当前时间）.
    /// </summary>
    [Description("range 模式的结束时间（RFC3339 或 Unix 秒）；缺省为当前时间")]
    public string? End { get; set; }

    /// <summary>
    /// range 模式的步长（可空，缺省由服务端决定）.
    /// </summary>
    [Description("range 模式的步长（Prometheus 持续时间字面量，如 1m、5m、1h）；缺省由服务端决定")]
    public string? Step { get; set; }

    /// <summary>
    /// label_values 模式：目标标签名.
    /// </summary>
    [Description("label_values 模式：目标标签名，例如 job")]
    public string? Label { get; set; }

    /// <summary>
    /// series 模式：series 选择器（match[]，可空）.
    /// </summary>
    [Description("series 模式：series 选择器（match[]），如 job=node；缺省时列出全部序列的标签集")]
    public string? Selector { get; set; }
}
