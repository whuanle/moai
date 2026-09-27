using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 瞬时向量条目：标签集 + 单点样本.
/// </summary>
public class PrometheusSample
{
    /// <summary>
    /// 标签集.
    /// </summary>
    [Description("标签集（标签 → 值），含 __name__ 指标名")]
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Unix 秒时间戳.
    /// </summary>
    [Description("样本时间（Unix 秒）")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 样本值（文本）.
    /// </summary>
    [Description("样本值文本（数值可能带 NaN/+Inf/-Inf）")]
    public string Value { get; set; } = string.Empty;
}
