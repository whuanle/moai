using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Tempo 链路的 span.
/// </summary>
public class TempoSpan
{
    /// <summary>
    /// SpanID.
    /// </summary>
    [Description("SpanID（hex）")]
    public string SpanId { get; set; } = string.Empty;

    /// <summary>
    /// 父 SpanID（根 span 为空）.
    /// </summary>
    [Description("父 SpanID（根 span 为空）")]
    public string ParentSpanId { get; set; } = string.Empty;

    /// <summary>
    /// span 名称.
    /// </summary>
    [Description("span 名称，如 HTTP GET /api/orders")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间（Unix 纳秒文本）.
    /// </summary>
    [Description("开始时间（Unix 纳秒文本）")]
    public string StartTimeUnixNano { get; set; } = string.Empty;

    /// <summary>
    /// 时长（纳秒文本）.
    /// </summary>
    [Description("时长（纳秒文本，由 endTimeUnixNano - startTimeUnixNano 求得）")]
    public string DurationNanos { get; set; } = string.Empty;

    /// <summary>
    /// 状态码（OTel StatusCode 文本）.
    /// </summary>
    [Description("状态码（1=Ok，2=Error，0/空=Unset）")]
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>
    /// 状态消息.
    /// </summary>
    [Description("状态消息（失败 span 的错误描述）")]
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// span 属性.
    /// </summary>
    [Description("span 属性（键 → 值；复杂值截断回写原文 JSON）")]
    public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
}
