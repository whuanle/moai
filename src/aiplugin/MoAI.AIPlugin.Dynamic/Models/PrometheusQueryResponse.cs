using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Prometheus 指标查询插件响应结果.
/// </summary>
public class PrometheusQueryResponse
{
    /// <summary>
    /// 结果形态.
    /// </summary>
    [Description("结果形态：vector（瞬时向量）/matrix（区间矩阵）/scalar/string（标量与字符串，见 Scalar）；labels/label_values/series/alerts/rules 模式分别填 Labels/Series/Alerts/Rules")]
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// 是否被行数/点数上限截断.
    /// </summary>
    [Description("是否因 MaxSeries/MaxListItems/MaxPointsPerSeries 被截断；true 表示服务端还有更多数据未返回")]
    public bool Truncated { get; set; }

    /// <summary>
    /// scalar/string 模式的求值点.
    /// </summary>
    [Description("scalar/string 结果（PromQL 标量表达式），Value 为数值文本（含 NaN/+Inf/-Inf）")]
    public PrometheusScalarValue? Scalar { get; set; }

    /// <summary>
    /// vector 结果：每条含标签集与单点样本.
    /// </summary>
    [Description("vector 结果：每条含标签集（Labels）与单点样本（Timestamp/Value）")]
    public IReadOnlyList<PrometheusSample> Vector { get; set; } = [];

    /// <summary>
    /// matrix 结果：每条含标签集与多点样本.
    /// </summary>
    [Description("matrix 结果：每条含标签集（Labels）与时间序列样本列表（Samples）")]
    public IReadOnlyList<PrometheusMatrixSeries> Matrix { get; set; } = [];

    /// <summary>
    /// labels 模式：标签名列表.
    /// </summary>
    [Description("labels 模式：标签名列表")]
    public IReadOnlyList<string> Labels { get; set; } = [];

    /// <summary>
    /// series 模式：序列标签集列表.
    /// </summary>
    [Description("series 模式：序列标签集列表（每项为标签 → 值）")]
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Series { get; set; } = [];

    /// <summary>
    /// alerts 模式：当前告警列表.
    /// </summary>
    [Description("alerts 模式：当前告警列表（State=firing/pending）")]
    public IReadOnlyList<PrometheusAlert> Alerts { get; set; } = [];

    /// <summary>
    /// rules 模式：规则组列表.
    /// </summary>
    [Description("rules 模式：规则组列表")]
    public IReadOnlyList<PrometheusRuleGroup> Rules { get; set; } = [];
}
