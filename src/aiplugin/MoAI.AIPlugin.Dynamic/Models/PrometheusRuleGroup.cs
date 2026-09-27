using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 规则组条目.
/// </summary>
public class PrometheusRuleGroup
{
    /// <summary>
    /// 规则文件.
    /// </summary>
    [Description("规则文件路径")]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// 规则组名.
    /// </summary>
    [Description("规则组名")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 规则组内的规则列表.
    /// </summary>
    [Description("规则组内的规则列表")]
    public IReadOnlyList<PrometheusRule> Rules { get; set; } = [];
}
