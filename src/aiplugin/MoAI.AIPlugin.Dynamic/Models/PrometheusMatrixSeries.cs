using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 区间矩阵条目：标签集 + 多点样本.
/// </summary>
public class PrometheusMatrixSeries
{
    /// <summary>
    /// 标签集.
    /// </summary>
    [Description("标签集（标签 → 值），含 __name__ 指标名")]
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 时间序列样本.
    /// </summary>
    [Description("时间序列样本列表（时间戳 Unix 秒 + 数值文本）")]
    public IReadOnlyList<PrometheusScalarValue> Samples { get; set; } = [];
}
