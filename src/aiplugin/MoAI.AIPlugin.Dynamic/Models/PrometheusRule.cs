using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 单条规则（记录规则或告警规则）.
/// </summary>
public class PrometheusRule
{
    /// <summary>
    /// 规则名.
    /// </summary>
    [Description("规则名（记录规则名或告警规则名 alertname）")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 类型.
    /// </summary>
    [Description("类型：recording=记录规则，alerting=告警规则")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 表达式.
    /// </summary>
    [Description("规则表达式（PromQL）")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 状态.
    /// </summary>
    [Description("告警规则的状态：inactive/pending/firing；记录规则为空")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 健康状态.
    /// </summary>
    [Description("最近一次求值的健康状态：ok 或 error")]
    public string Health { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次求值错误.
    /// </summary>
    [Description("最近一次求值错误信息（health=error 时非空）")]
    public string LastError { get; set; } = string.Empty;

    /// <summary>
    /// 规则标签.
    /// </summary>
    [Description("规则标签")]
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 规则注解.
    /// </summary>
    [Description("规则注解（含 summary、description 等）")]
    public IReadOnlyDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
}
