using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Grafana Tempo 链路查询插件响应结果.
/// </summary>
public class TempoQueryResponse
{
    /// <summary>
    /// 实际使用的操作模式.
    /// </summary>
    [Description("实际使用的操作模式：traceql/trace/tags/tag_values")]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// 是否被上限截断.
    /// </summary>
    [Description("是否因 MaxTraces/MaxSpans 被截断；true 表示服务端还有更多数据未返回")]
    public bool Truncated { get; set; }

    /// <summary>
    /// traceql 模式：命中链路摘要列表.
    /// </summary>
    [Description("traceql 模式：命中链路摘要（TraceId/根服务/根名称/开始时间/时长毫秒）")]
    public IReadOnlyList<TempoTraceSummary> Traces { get; set; } = [];

    /// <summary>
    /// trace 模式：链路的资源与 span（按 batch 分组）.
    /// </summary>
    [Description("trace 模式：链路详情（每条 batch 含资源属性与 spans 列表）")]
    public IReadOnlyList<TempoBatch> Batches { get; set; } = [];

    /// <summary>
    /// tags 模式：可检索标签名列表.
    /// </summary>
    [Description("tags 模式：可检索的标签名列表")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>
    /// tag_values 模式：标签取值列表.
    /// </summary>
    [Description("tag_values 模式：目标标签的取值列表")]
    public IReadOnlyList<string> TagValues { get; set; } = [];
}
