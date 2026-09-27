using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Grafana Tempo 链路查询插件请求参数.
/// </summary>
public class TempoQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：traceql=TraceQL 检索（默认）/ trace=按 traceID 取链路详情 / tags=可检索标签名列表 / tag_values=某标签的取值列表；未知值会被拒绝")]
    public string Mode { get; set; } = "traceql";

    /// <summary>
    /// TraceQL 表达式（traceql 模式必填）.
    /// </summary>
    [Description("traceql 模式：TraceQL 表达式，如 {service.name=\"api\" && duration>1s}、{resource.namespace=\"prod\"}")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// traceql 模式最多返回链路数（可空，缺省用配置）.
    /// </summary>
    [Description("traceql 模式最多返回链路数，取值 1-100（缺省用配置 MaxTraces）")]
    public int? Limit { get; set; }

    /// <summary>
    /// 检索开始时间（可空）.
    /// </summary>
    [Description("traceql 模式：开始时间（RFC3339 或 Unix 秒）")]
    public string? Start { get; set; }

    /// <summary>
    /// 检索结束时间（可空）.
    /// </summary>
    [Description("traceql 模式：结束时间（RFC3339 或 Unix 秒）")]
    public string? End { get; set; }

    /// <summary>
    /// trace 模式：目标 TraceID.
    /// </summary>
    [Description("trace 模式：目标 TraceID（hex）")]
    public string? TraceId { get; set; }

    /// <summary>
    /// tag_values 模式：目标标签名（可空，默认 service.name）.
    /// </summary>
    [Description("tag_values 模式：目标标签名，默认 service.name（用于列出全部服务）")]
    public string? Tag { get; set; }
}
